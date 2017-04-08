﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Pageant;

namespace NovaSFTP2.Model {
	class SFTPFileUploader {
		public SFTPFileUploader() {
			upload_thread = new Thread(UploadLoop);
			upload_thread.Name = "BackgroundUploader";
			upload_thread.IsBackground = true;
			upload_thread.Start();
		}
		private Thread upload_thread;
		private SftpClient client;
		private string base_path;
		private int base_path_len;
		public async Task connect(String host, int port, String user, String local_path, String remote_path, String password) {
			base_path = local_path;
			base_path_len = (base_path.EndsWith("/") || base_path.EndsWith("\\")) ? base_path.Length : base_path.Length + 1; //if it doesnt end in a slash we need to skip it
			if (String.IsNullOrWhiteSpace(user)) {
				MainWindow.ShowMessage("No user specified", "Missing Username");
				return;
			}
			if (password == null) {
				var agent = new PageantProtocol();
				var conn = new AgentConnectionInfo(host, port, user, agent);
				client = new SftpClient(conn);
			} else {
				client = new SftpClient(host, port, user, password);
			}
			client.KeepAliveInterval = TimeSpan.FromSeconds(30);
			client.ErrorOccurred += client_ErrorOccurred;
			try {
				client.Connect();
				if (!String.IsNullOrWhiteSpace(remote_path))
					client.ChangeDirectory(remote_path);
				ConnChanged();
			} catch (SocketException e) {
				await disconnect();
				MainWindow.ShowMessage("Unable to connect due to socket exception of: " + e.Message, "Connection Error");
			} catch (SshAuthenticationException e) {
				await disconnect();
				MainWindow.ShowMessage("Unable to connect due to auth exception of: " + e.Message, "Connection Error");
			} catch (SftpPathNotFoundException) {
				await disconnect();
				MainWindow.ShowMessage("Unable to switch to remote folder of: " + remote_path + " as it doesn't exist", "Connection Error");
			}
		}
		private void ConnChanged() {
			if (ConnectedChanged != null)
				ConnectedChanged(null, null);
		}
		public EventHandler ConnectedChanged;
		public bool is_connected {
			get { return client != null && client.IsConnected; }
		}
		async void client_ErrorOccurred(object sender, Renci.SshNet.Common.ExceptionEventArgs e) {
			try {
				throw e.Exception;
			} catch (SocketException exp) {
				await disconnect();
				MainWindow.ShowMessage("Connection lost due to " + exp.SocketErrorCode + ": " + exp.Message, "Connection Error");
			} catch (SshConnectionException ss_exp) {
				await disconnect();
				MainWindow.ShowMessage("Connection issue due to " + ss_exp.DisconnectReason + ": " + ss_exp.Message, "Connection Error");
			} catch (Exception ee) {
				await disconnect();
				if (Debugger.IsAttached)
					throw ee;
				MainWindow.ShowMessage("unknown error let us know: " + ee.Message, "Unknown Error");
			}
		}
		private void UploadCallback(ulong progress, ulong total_size) {
			//WindowsFormsExtensions.SetTaskbarProgress(this, (((float)transferredBytes) / totalBytes) * 100);
			var evt = new UploadProgressEvtArgs { total_bytes = total_size, uploaded_bytes = progress };
			if (UploadEvtProgress != null)
				UploadEvtProgress(this, evt);
		}
		ConcurrentQueue<string> CurQueue = new ConcurrentQueue<string>();
		private void UploadLoop() {//don't make async as then lose thread name
			string file;
			string last_file = null;
			int was_connected_cnt = 0;
			while (true) {
				try {
					while (CurQueue.TryDequeue(out file)) {
						if (!is_connected) {
							was_connected_cnt = 0;
							CurQueue = new ConcurrentQueue<string>();
							disconnect().Wait();
							MainWindow.ShowMessage("Connection to server lost.", "Lost Connection"); //maybe not supposed to ever happen? should have been caught sooner
							break;
						}
						was_connected_cnt = 1;
						if (file == last_file)
							Task.Delay(50).Wait();
						last_file = file;
						if ((DateTime.Now - last_changed[file]).TotalMilliseconds < 50) {
							CurQueue.Enqueue(file);
							continue;
						}
						var remote_name = file.Substring(base_path_len);
						remote_name = remote_name.Replace('\\', '/');
						if (CurQueue.Contains(file)) //continue if it was added to the queue again since we started
							continue;
						UploadFile(file, remote_name).Wait();
						if (!CurQueue.Contains(file))
							last_changed.Remove(file);

					}
					bool is_conn = false;
					if (was_connected_cnt > 0 && was_connected_cnt++ < 60 * 100 || (is_conn = is_connected)) {
						//so isconnected is a bit of a possible costly call so lets not call it every 50ms.  Lets cache for up to 5 minutes
						if (is_conn)
							was_connected_cnt = 1;
						Task.Delay(50).Wait();
					} else
						Task.Delay(1000).Wait();
				}catch(Exception e) {
					MessageBox.Show("Exception in upload loop should attach debugger and figure out why this is not caught currently: " + e.Message + "\n" + e.StackTrace);
				}
			}
		}
		private async Task UploadFile(String filename, String remote_name) {
			var stat = new FileInfo(filename);
			try {
				using (var file = File.OpenRead(filename)) {
					client.UploadFile(file, remote_name, true, l => UploadCallback(l, (ulong)stat.Length));
				}
			} catch (FileNotFoundException) { } //moved or deleted before we uplaoded this is ok
			catch (IOException e) {
				if (e.Message.Contains("used by another process")) {
					await Task.Delay(100);
					if (!CurQueue.Contains(filename))
						CurQueue.Enqueue(filename);
					return;
				}
				throw e;
			} catch (SshConnectionException e) {
				await disconnect();
				MainWindow.ShowMessage("Connection to server lost details: " + e.Message, "Lost Connection");
			} catch (SftpPermissionDeniedException e) {
				await disconnect();
				MainWindow.ShowMessage("Permission denied trying to upload due to: " + e.Message, "Permission Error");
			} catch (SftpPathNotFoundException e) {
				await disconnect();
				MainWindow.ShowMessage($"Remote file not accessible, most likely invalid remote path(make sure folder exists): {remote_name}", "Path Not Found Error");
			} catch (SshException e) {
				if (e.Message == "Channel was closed.") {
					await disconnect();
					MainWindow.ShowMessage("Connection to server lost details: " + e.Message, "Lost Connection");
				} else if (e.Message == "Failure.") {
					await disconnect();
					MainWindow.ShowMessage("General failure from SSH Libary", "General Failure");
				} else
					throw e;
			}
			}
		public async Task disconnect() {
			if (client != null) {
				try {
					var tsk = Task.Run(() => {
						if (client.IsConnected) //otherwise it can hang forever
							client.Disconnect();
						if (client.IsConnected)
							client.Disconnect();
					});
					await Task.WhenAny(tsk, Task.Delay(60 * 1000));
					if (!tsk.IsCompleted) {
						MessageBox.Show("Prevented a hang on disconnect");
					}
				} catch (Exception e) {
					Debug.WriteLine("Unable to disconnect due to: " + e.Message);
				}
			}
			ConnChanged();
		}
		private Dictionary<string, DateTime> last_changed = new Dictionary<string, DateTime>();
		public void AddFileUpload(String local_file) {
			if (Directory.Exists(local_file))
				return;
			if (ignore_regex.IsMatch(local_file))
				return;
			last_changed[local_file] = DateTime.Now;
			if (!CurQueue.Contains(local_file))
				CurQueue.Enqueue(local_file);
		}
		public class UploadProgressEvtArgs {
			public ulong total_bytes;
			public ulong uploaded_bytes;
		}
		public EventHandler<UploadProgressEvtArgs> UploadEvtProgress;
		private Regex ignore_regex;
		public void SetRegex(string ignore_regex_str) {
			ignore_regex = new Regex(ignore_regex_str, RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		}
	}
}
