using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NovaSFTP2.ViewModel;

namespace NovaSFTP2.Model {
	public class UploadProgressEvtArgs {
		public ulong total_bytes;
		public ulong uploaded_bytes;
	}
	public abstract class BaseFileUploader {
		protected BaseFileUploader(UPLOADER_TYPE type) {
			upload_thread = new Thread(UploadLoop);
			upload_thread.Name = type + " BackgroundUploader";
			upload_thread.IsBackground = true;
			upload_thread.Start();
		}
		public EventHandler ConnectedChanged { get; set; }
		public abstract bool is_connected { get; }
		public EventHandler<UploadProgressEvtArgs> UploadEvtProgress;
		public abstract Task disconnect();
		private Thread upload_thread;
		ConcurrentQueue<string> CurQueue = new ConcurrentQueue<string>();
		private Dictionary<string, DateTime> last_changed = new Dictionary<string, DateTime>();
		protected void SetLocalPath(String local_path) {
			base_path = local_path;
			base_path_len = (base_path.EndsWith("/") || base_path.EndsWith("\\")) ? base_path.Length : base_path.Length + 1; //if it doesnt end in a slash we need to skip it
		}
		protected abstract Task UploadFile(Stream file, String remote_name);

		public void AddFileUpload(String local_file) {
			if (!upload_thread.IsAlive)
				MainWindow.ShowMessage("Background thread is not alive something very bad happened","Exit App");
			if (Directory.Exists(local_file))
				return;
			if (ignore_regex.IsMatch(local_file))
				return;
			last_changed[local_file] = DateTime.Now;
			if (!CurQueue.Contains(local_file))
				CurQueue.Enqueue(local_file);
		}

		private Regex ignore_regex;
		public void SetRegex(string ignore_regex_str) {
			ignore_regex = new Regex(ignore_regex_str, RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
		}
		protected void ConnChanged() {
			ConnectedChanged?.Invoke(null, null);
		}
		private string base_path;
		private int base_path_len;
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
						try {
							using (var stream = File.OpenRead(file)) {
								UploadFile(stream, remote_name).Wait();
							}
						} catch (FileNotFoundException) { } //moved or deleted before we uplaoded this is ok
						catch (IOException e) {
							if (e.Message.Contains("used by another process")) {
								Task.Delay(100).Wait();
								if (!CurQueue.Contains(file))
									CurQueue.Enqueue(file);
								return;
							}
							throw e;
						}
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
				} catch (Exception e) {
					MessageBox.Show("Exception in upload loop should attach debugger and figure out why this is not caught currently: " + e.Message + "\n" + e.StackTrace);
				}
			}
		}
	}
}