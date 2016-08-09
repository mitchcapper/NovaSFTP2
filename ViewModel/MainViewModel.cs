using System;
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

namespace NovaSFTP2.ViewModel {
	class MainViewModel : ViewModelBase {
		private SFTPFileUploader uploader;
		private FileSystemWatcher watcher;
		public MainViewModel() {
			var settings = HostInfo.LoadSettings();
			hosts = new ObservableCollection<HostInfo>(settings.hosts);
			if (hosts.Count == 0)
				hosts.Add(new HostInfo { });
			selected_host = hosts.FirstOrDefault(a => String.IsNullOrWhiteSpace(a.name));
			if (selected_host == null) 
				hosts.Insert(0, (selected_host= new HostInfo { }));
			uploader = new SFTPFileUploader();
			uploader.UploadEvtProgress += UploadEvtProgress;
			uploader.ConnectedChanged += ConnectedChanged;
			watcher = new FileSystemWatcher();
			watcher.NotifyFilter = NotifyFilters.LastWrite;
			watcher.Changed += FileChanged;
			UpdateButton();
			try {
				ignore_regex = settings.ignore_regex;
			} catch (Exception) {}
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

		private void ConnectedChanged(object sender, EventArgs event_args) {
			Debug.WriteLine("Connection changed status is: " + connected);
			UpdateButton();
			if (connected)
				StartWatcher();
			else
				StopWatcher();
		}

		private void StartWatcher() {
			if (local_folder.EndsWith("\\"))
				watcher.Path = local_folder.Substring(0, local_folder.Length - 1);
			else
				watcher.Path = local_folder;

			watcher.IncludeSubdirectories = include_subfolders;

			try {
				watcher.EnableRaisingEvents = true;
			} catch (ArgumentException e) {
				disconnect();
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

		}
		private void connect() {
			uploader.connect(hostname, port, username, local_folder, remote_folder, password);

			if (String.IsNullOrWhiteSpace(selected_host?.name) == false)
				UpdateRecent(selected_host.name);
		}
		private void disconnect() {
			StopWatcher();
			uploader.disconnect();
		}
		private bool connected { get { return uploader.is_connected; } }
		public ICommand ToggleConnectedCmd => new OurCommand(ToggleConnected,true,true);
		private void ToggleConnected() {
			if (connected)
				disconnect();
			else
				connect();
		}
		private void FileChanged(object sender, FileSystemEventArgs args) {
			if (args.ChangeType == WatcherChangeTypes.Deleted)
				return;
			uploader.AddFileUpload(args.FullPath);
		}

		private void UploadEvtProgress(object sender, SFTPFileUploader.UploadProgressEvtArgs args) {
			if (ProgressMade == null)
				return;
			ProgressMade(this, ((double)args.uploaded_bytes) / args.total_bytes);
		}

		public ICommand FavDelCmd => new OurCommand(FavDel);
		private async Task FavDel() {
			if (hosts.IndexOf(selected_host) == 0) {
				MainWindow.ShowMessage("Cannot delete the defaults host","Cannot Delete");
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
			selected_host.host = hostname;
			selected_host.port = port;
			selected_host.localFolder = local_folder;
			selected_host.recursive = include_subfolders;
			selected_host.remoteFolder = remote_folder;
			selected_host.username = username;
			await HostInfo.SaveSettings(new SettingsInfo {ignore_regex = ignore_regex,hosts = hosts.ToArray() });
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
					hostname = selected_host.host;
					port = selected_host.port;
					local_folder = selected_host.localFolder;
					include_subfolders = selected_host.recursive;
					remote_folder = selected_host.remoteFolder;
					username = selected_host.username;
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
					throw new ArgumentException(e.Message,e);
				}
				if (Set(() => ignore_regex, ref _ignore_regex, value)) {
					uploader.SetRegex(ignore_regex);
				}

			}
		}
		private string _ignore_regex;


	}
}
