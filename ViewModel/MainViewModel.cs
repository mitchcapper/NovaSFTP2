﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Threading;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using NovaSFTP2.Model;
using Docker.DotNet.X509;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Docker.DotNet;
using Docker.DotNet.Models;
using SharpCompress.Writers;
using SharpCompress.Common;
using System.Net;
using System.Net.Security;
using SharpCompress.Archives.Tar;
using System.Collections.Generic;

namespace NovaSFTP2.ViewModel {
	public enum UPLOADER_TYPE { SFTP, DOCKER }

	public enum TLS_MODE { None, Ignore_Hostname_Mismatch, Required }

	class MainViewModel : ViewModelBase {
		public IEnumerable<UPLOADER_TYPE> UPLOAD_TYPES => Enum.GetValues(typeof(UPLOADER_TYPE)).Cast<UPLOADER_TYPE>();
		public IEnumerable<TLS_MODE> TLS_MODES => Enum.GetValues(typeof(TLS_MODE)).Cast<TLS_MODE>();
		public Dictionary<UPLOADER_TYPE, BaseFileUploader> uploaders = new Dictionary<UPLOADER_TYPE, BaseFileUploader>();
		private BaseFileUploader uploader;
		private FileSystemWatcher watcher;
		private string default_ca_path;
		private string default_key_path;
		public MainViewModel() {
			var user_path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			default_ca_path = user_path + "\\.docker\\ca.pem";
			default_key_path = user_path + "\\.docker\\key.pfx";
			var settings = HostInfo.LoadSettings();
			hosts = new ObservableCollection<HostInfo>(settings.hosts);
			if (hosts.Count == 0)
				hosts.Add(new HostInfo { });
			selected_host = hosts.FirstOrDefault(a => String.IsNullOrWhiteSpace(a.name));
			if (selected_host == null)
				hosts.Insert(0, (selected_host = new HostInfo { }));
			uploaders[UPLOADER_TYPE.SFTP] = new SFTPFileUploader();
			uploaders[UPLOADER_TYPE.DOCKER] = new DockerFileUploader();

			foreach (var uploader in uploaders.Values) {
				uploader.UploadEvtProgress += UploadEvtProgress;
				uploader.ConnectedChanged += ConnectedChanged;
			}
			watcher = new FileSystemWatcher();
			watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
			watcher.Changed += FileChanged;
			watcher.Renamed += FileRenamed;
			watcher.Created += FileCreated;
			UpdateButton();
			try {
				ignore_regex = settings.ignore_regex;
			} catch (Exception) { }
			//test_perf();
		}
		private async Task TimeTask(String name, Func<Task<long>> t) {
			var start = DateTime.Now;
			var len = await t();
			var time = DateTime.Now - start;
			Debug.WriteLine(name + "for " + len + " Took : " + time.TotalSeconds);
		}

		public Visibility show_docker_options => upload_type == UPLOADER_TYPE.DOCKER ? Visibility.Visible : Visibility.Collapsed;
		public Visibility show_sftp_options => upload_type == UPLOADER_TYPE.SFTP ? Visibility.Visible : Visibility.Collapsed;
		public TLS_MODE tls_mode {
			get { return _tls_mode; }
			set { Set(() => tls_mode, ref _tls_mode, value); }
		}
		private TLS_MODE _tls_mode = TLS_MODE.Required;
		public UPLOADER_TYPE upload_type {
			get { return _upload_type; }
			set {
				if (Set(() => upload_type, ref _upload_type, value)) {
					if (upload_type == UPLOADER_TYPE.DOCKER && username == "root") {
						username = default_ca_path;
						password = default_key_path;
					} else if (upload_type == UPLOADER_TYPE.SFTP && username == default_ca_path) {
						username = "root";
						if (password == default_key_path)
							password = "";
					}
					RaisePropertyChanged(() => show_docker_options);
					RaisePropertyChanged(() => show_sftp_options);
				}

			}
		}
		private UPLOADER_TYPE _upload_type = UPLOADER_TYPE.DOCKER;

		private void FileCreated(object sender, FileSystemEventArgs e) {
			uploader.AddFileUpload(e.FullPath);
		}

		private void FileRenamed(object sender, RenamedEventArgs e) {
			uploader.AddFileUpload(e.FullPath);
		}

		public void loaded() {
			var args = Environment.GetCommandLineArgs();
			if (args.Length == 2) {
				var load = hosts.FirstOrDefault(a => a.name == args[1]); ;
				if (load != null) {
					selected_host = load;
					ToggleConnectedCmd.Execute(null);
				}
			}
		}
		public string title {
			get { return _title; }
			set { Set(() => title, ref _title, value); }
		}
		private string _title;

		public bool use_compression {
			get { return _use_compression; }
			set { Set(() => use_compression, ref _use_compression, value); }
		}
		private bool _use_compression;
		private void ConnectedChanged(object sender, EventArgs event_args) {
			UpdateButton();
			if (connected)
				StartWatcher();
			else
				StopWatcher();
		}

		private async void StartWatcher() {
			if (local_folder.EndsWith("\\"))
				watcher.Path = local_folder.Substring(0, local_folder.Length - 1);
			else
				watcher.Path = local_folder;

			watcher.IncludeSubdirectories = include_subfolders;

			try {
				watcher.EnableRaisingEvents = true;
			} catch (ArgumentException e) {
				await disconnect();
				MainWindow.ShowMessage("Unable to watch local folder due to: " + e.Message, "Invalid Local Folder");
			}
		}
		private void StopWatcher() {
			watcher.EnableRaisingEvents = false;
		}
		private void UpdateButton() {
			action_button_content = !connected ? "Connect" : "Disconnect";
			var t_str = "NovaSFTP2";
			if (connected)
				t_str += " - " + hostname;
			title = t_str;
			if (!connected)
				type_selector_enabled = true;
		}
		public bool type_selector_enabled {
			get { return _type_selector_enabled; }
			set { Set(() => type_selector_enabled, ref _type_selector_enabled, value); }
		}
		private bool _type_selector_enabled;
		private async Task connect() {
			type_selector_enabled = false;
			uploader = uploaders[upload_type];
			switch (upload_type) {
				case UPLOADER_TYPE.SFTP:
					await (uploader as SFTPFileUploader).connect(hostname, port, username, local_folder, remote_folder, password);
					break;
				case UPLOADER_TYPE.DOCKER:
					await (uploader as DockerFileUploader).connect(hostname, port, username, local_folder, remote_folder, password, tls_mode, use_compression, container);
					break;
			}
			if (!uploader.is_connected)
				type_selector_enabled = true;
			if (String.IsNullOrWhiteSpace(selected_host?.name) == false)
				UpdateRecent(selected_host.name);
		}
		private async Task disconnect() {
			StopWatcher();
			await uploader.disconnect();
		}
		private bool connected { get { return uploader?.is_connected ?? false; } }
		public ICommand ToggleConnectedCmd => new OurCommand(ToggleConnected, true);
		private async Task ToggleConnected() {
			if (connected)
				await disconnect();
			else
				await Task.Run(async () => await connect());
		}
		private void FileChanged(object sender, FileSystemEventArgs args) {
			if (args.ChangeType == WatcherChangeTypes.Deleted)
				return;
			uploader.AddFileUpload(args.FullPath);
		}

		private void UploadEvtProgress(object sender, UploadProgressEvtArgs args) {
			if (ProgressMade == null)
				return;
			ProgressMade(this, ((double)args.uploaded_bytes) / args.total_bytes);
		}

		public ICommand FavDelCmd => new OurCommand(FavDel);
		private async Task FavDel() {
			if (hosts.IndexOf(selected_host) == 0) {
				MainWindow.ShowMessage("Cannot delete the defaults host", "Cannot Delete");
				return;
			}
			hosts.Remove(selected_host);
			await HostInfo.SaveSettings(new SettingsInfo { ignore_regex = ignore_regex, hosts = hosts.ToArray() });
		}
		public ICommand FavSaveCmd => new OurCommand(FavSave);
		private async Task FavSave() {
			if (selected_host == null)
				return;
			if (hosts.IndexOf(selected_host) == 0) {
				var res = MessageBox.Show("Are you sure you want to update the default host?", "Confirm default override", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
				if (res != MessageBoxResult.Yes)
					return;
			}
			selected_host.upload_type = upload_type;
			selected_host.host = hostname;
			selected_host.port = port;
			selected_host.localFolder = local_folder;
			selected_host.recursive = include_subfolders;
			selected_host.remoteFolder = remote_folder;
			selected_host.username = username;
			if (File.Exists(password))
				selected_host.password = password;
			selected_host.container = container;
			selected_host.tls_mode = tls_mode;
			selected_host.use_compression = use_compression;
			await HostInfo.SaveSettings(new SettingsInfo { ignore_regex = ignore_regex, hosts = hosts.ToArray() });
		}

		public ICommand FavSaveAsCmd => new OurCommand(FavSaveAs);


		private async Task FavSaveAs() {
			var name = InputWindow.GetInput("Save As Name", selected_host?.name);
			if (String.IsNullOrWhiteSpace(name))
				return;
			_selected_host = new HostInfo { name = name };
			hosts.Add(_selected_host);
			await FavSave();
		}
		public EventHandler<double> ProgressMade;
		private void UpdateRecent(String name) {
			String at_bottom = "";
			if (File.Exists(recent_file)) {
				TextReader tr = new StreamReader(recent_file);
				string line;
				int cnt = 0;
				while ((line = tr.ReadLine()) != null) {
					if (line == name)
						continue;
					if (++cnt == 20)
						break;
					at_bottom += "\n" + line;
				}
				tr.Close();
			}
			TextWriter tw = new StreamWriter(recent_file);
			tw.Write(name + at_bottom);
			tw.Close();
			dispatcher?.BeginInvoke((Action)UpdateJumpList);

		}

		public Dispatcher dispatcher;
		private readonly string recent_file = HostInfo.GetUserAppDataPath() + "\\program.recent";
		private void UpdateJumpList() {
			var list = new JumpList();
			var app_path = System.Reflection.Assembly.GetExecutingAssembly().Location;
			if (File.Exists(recent_file)) {
				TextReader tr = new StreamReader(recent_file);
				string line;
				while ((line = tr.ReadLine()) != null) {
					list.JumpItems.Add(new JumpTask {
						ApplicationPath = app_path, Arguments = "\"" + line + "\"",
						CustomCategory = "Recent Monitors", Title = line,
						IconResourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll"), IconResourceIndex = 12
					});
				}
				tr.Close();
				JumpList.SetJumpList(Application.Current, list);
			}
		}
		public string container {
			get { return _container; }
			set { Set(() => container, ref _container, value); }
		}
		private string _container;
		public string password {
			get { return _password; }
			set { Set(() => password, ref _password, value); }
		}
		private string _password;

		public int port {
			get { return _port; }
			set { Set(() => port, ref _port, value); }
		}
		private int _port = 22;

		public string hostname {
			get { return _hostname; }
			set { Set(() => hostname, ref _hostname, value); }
		}
		private string _hostname = "";

		public string username {
			get { return _username; }
			set { Set(() => username, ref _username, value); }
		}
		private string _username = "root";


		public ObservableCollection<HostInfo> hosts {
			get { return _hosts; }
			set { Set(() => hosts, ref _hosts, value); }
		}
		private ObservableCollection<HostInfo> _hosts;


		public HostInfo selected_host {
			get { return _selected_host; }
			set {
				if (Set(() => selected_host, ref _selected_host, value) && value != null) {
					upload_type = selected_host.upload_type;
					hostname = selected_host.host;
					port = selected_host.port;
					local_folder = selected_host.localFolder;
					include_subfolders = selected_host.recursive;
					remote_folder = selected_host.remoteFolder;
					container = selected_host.container;
					tls_mode = selected_host.tls_mode;
					upload_type = selected_host.upload_type;
					use_compression = selected_host.use_compression;
					username = selected_host.username;
					password = selected_host.password;
				}

			}
		}
		private HostInfo _selected_host;


		public string remote_folder {
			get { return _remote_folder; }
			set { Set(() => remote_folder, ref _remote_folder, value); }
		}
		private string _remote_folder;

		public string local_folder {
			get { return _local_folder; }
			set { Set(() => local_folder, ref _local_folder, value); }
		}
		private string _local_folder;

		public bool include_subfolders {
			get { return _include_subfolders; }
			set { Set(() => include_subfolders, ref _include_subfolders, value); }
		}
		private bool _include_subfolders;

		public string action_button_content {
			get { return _action_button_content; }
			set { Set(() => action_button_content, ref _action_button_content, value); }
		}
		private string _action_button_content;


		public string ignore_regex {
			get { return _ignore_regex; }
			set {
				try {
					var ex = new Regex(value, RegexOptions.IgnorePatternWhitespace);
				} catch (Exception e) {
					throw new ArgumentException(e.Message, e);
				}
				if (Set(() => ignore_regex, ref _ignore_regex, value)) {
					foreach (var uploader in uploaders.Values)
						uploader.SetRegex(ignore_regex);
				}

			}
		}
		private string _ignore_regex;


	}
}
