using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using NovaSFTP2.ViewModel;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Pageant;

namespace NovaSFTP2.Model {
	

	class SFTPFileUploader : BaseFileUploader {
		public SFTPFileUploader() : base(UPLOADER_TYPE.SFTP) { }
		private SftpClient client;
		public async Task connect(String host, int port, String user, String local_path, String remote_path, String password) {
			SetLocalPath(local_path);
			if (String.IsNullOrWhiteSpace(user)) {
				MainWindow.ShowMessage("No user specified", "Missing Username");
				return;
			}
			if (String.IsNullOrWhiteSpace(password)) {
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

		public override bool is_connected => client != null && client.IsConnected;

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
			UploadEvtProgress?.Invoke(this, evt);
		}
		protected override async Task UploadFile(Stream file, String remote_name) {
			try {
				var total_size = (ulong)file.Length;//we need to cache this is technically it could be disposed right before the callback is called:)
				client.UploadFile(file, remote_name, true, l => UploadCallback(l, total_size));
				//ObjectDisposedException
			} catch (SshConnectionException e) {
				await disconnect();
				MainWindow.ShowMessage("Connection to server lost details: " + e.Message, "Lost Connection");
			} catch (SftpPermissionDeniedException e) {
				await disconnect();
				MainWindow.ShowMessage("Permission denied trying to upload due to: " + e.Message, "Permission Error");
			} catch (SftpPathNotFoundException) {
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


		public override async Task disconnect() {
			if (client != null) {
				try {
					var tsk = Task.Run(() => {
						if (client.IsConnected) //otherwise it can hang forever
							client.Disconnect();
						if (client.IsConnected)
							client.Disconnect();
					});
					await await Task.WhenAny(tsk, Task.Delay(60 * 1000));
					if (!tsk.IsCompleted) {
						MessageBox.Show("Prevented a hang on disconnect");
					}
				} catch (Exception e) {
					Debug.WriteLine("Unable to disconnect due to: " + e.Message);
				}
			}
			ConnChanged();
		}

	}
}
