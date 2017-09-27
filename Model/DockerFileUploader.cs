using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.BasicAuth;
using Docker.DotNet.Models;
using Docker.DotNet.X509;
using NovaSFTP2.ViewModel;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace NovaSFTP2.Model {
	class DockerFileUploader : BaseFileUploader, IDisposable {
		public DockerFileUploader() : base(UPLOADER_TYPE.DOCKER) { }
		private bool compress;
		private string ca_cert_path;
		private string container;
		private TLS_MODE tls_mode;
		private DockerClient client;
		private string remote_path;
		public async Task connect(String host, int port, String user, String local_path, String remote_path, String password, TLS_MODE tls_mode, bool use_compression, String container) {
			cancel = new CancellationTokenSource();
			SetLocalPath(local_path);
			compress = use_compression;
			this.tls_mode = tls_mode;
			if (String.IsNullOrWhiteSpace(remote_path))
				remote_path = "";
			else if (remote_path.EndsWith("/") == false && remote_path.EndsWith("\\") == false)
				remote_path += "/";
			this.remote_path = remote_path;
			this.container = container;
			if (host.IndexOf("://") == -1)
				host = "tcp://" + host;
			host += ":" + port;
			Credentials creds = new AnonymousCredentials();
			if (File.Exists(password)) {
				creds = new CertificateCredentials(new X509Certificate2(password, "")); //warning sym links will throw an error here
				ca_cert_path = user;
				((CertificateCredentials)creds).ServerCertificateValidationCallback += ServerCertificateValidationCallback;//not sure why cannot do this for basic auth
			} else if (!String.IsNullOrWhiteSpace(user) && !String.IsNullOrWhiteSpace(password)) {
				creds = new BasicAuthCredentials(user, password, tls_mode != TLS_MODE.None);
			}
			var config = new DockerClientConfiguration(new Uri(host), creds);
			client = config.CreateClient();
			try {
				var stats = await client.Containers.InspectContainerAsync(container);
				if (!stats.State.Running)
					MainWindow.ShowMessage("Container is not running", "Unable to connect");
				else
					connected = true;

			} catch (Exception e) {
				HandleException(e);
			}
		}

		private void HandleException(Exception ex) {
			var exp_msg = "Error of: " + ex.Message + ((ex.InnerException != null) ? " - " + ex.InnerException.Message : "");
			try {
				switch (ex) {
					case AuthenticationException e:
						MainWindow.ShowMessage("Make sure you have the CA specified if not in the computers CA store: " + exp_msg, "Authentication Exception");
						return;
					case DockerContainerNotFoundException e:
						MainWindow.ShowMessage("Container not found", "No Such Container");
						return;
					case HttpRequestException e:
						if (e.InnerException is IOException) {
							if (e.InnerException.Message == "Unexpected end of stream")
								MainWindow.ShowMessage("Unexpected stream end, try to make sure it is not supposed to be tls(or you forgot username/password)", "Request Exception");
							if (e.InnerException.Message == "Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.")
								MainWindow.ShowMessage("Connection closed by remote host, make sure the path exists you are uploading to (and it is not a file).","Request Exception");
						} else
							MainWindow.ShowMessage(exp_msg, "Request Exception");
						return;
				}
			} finally {
				connected = false;
			}
			throw ex;
		}

		private bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors ssl_policy_errors) {
			if (String.IsNullOrWhiteSpace(ca_cert_path)) {
				if (ssl_policy_errors == SslPolicyErrors.None)
					return true;
				return false;
			}
			var cert = new X509Certificate2(ca_cert_path); //warning sym links will throw an error here
			switch (ssl_policy_errors) {
				case System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors:
					break;
				case SslPolicyErrors.RemoteCertificateNameMismatch:
					if (tls_mode == TLS_MODE.Ignore_Hostname_Mismatch)
						break;
					return false;
				default:
					return false;
			}
			X509Chain chain0 = new X509Chain();
			chain0.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
			// add all your extra certificate chain
			chain0.ChainPolicy.ExtraStore.Add(cert);
			chain0.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
			return chain0.Build((X509Certificate2)certificate);

		}

		private bool _connected;
		private bool connected {
			get { return _connected; }
			set {
				if (_connected == value)
					return;
				_connected = value; ConnChanged();
			}
		}
		public override bool is_connected => connected;
		public override Task disconnect() {
			cancel.Cancel();
			connected = false;
			return Task.FromResult(false);
		}

		private CancellationTokenSource cancel;
		protected override async Task UploadFile(Stream file, string remote_name) {
			try {
				remote_name = remote_path + remote_name;
				using (Stream stream = new MemoryStream()) {
					var use_compression = compress;
					if (file.Length > 1024 * 1024 * 5) //if more than 5 megs don't comress
						use_compression = false;
					var writer = WriterFactory.Open(stream, ArchiveType.Tar, new WriterOptions(use_compression ? CompressionType.BZip2 : CompressionType.None) {LeaveStreamOpen = true});
					var arr = remote_name.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
					var path = String.Join("/", arr.Take(arr.Length - 1));

					writer.Write(arr[arr.Length - 1], file);
					writer.Dispose();
					var len = stream.Length;
					stream.Seek(0, SeekOrigin.Begin);
					UploadEvtProgress?.Invoke(this, new UploadProgressEvtArgs {total_bytes = 100, uploaded_bytes = 1});
					try {
						await client.Containers.ExtractArchiveToContainerAsync(container, new ContainerPathStatParameters() {Path = path}, stream, cancel.Token);
					} catch (Exception ex) {
						if (ex is TaskCanceledException || ex.InnerException is TaskCanceledException)
							return;
						throw ex;
					}
					UploadEvtProgress?.Invoke(this, new UploadProgressEvtArgs {total_bytes = 1, uploaded_bytes = 1});
				}
				
		} catch (Exception e) {
				HandleException(e);
			}
		}

		protected virtual void Dispose(bool disposing) {
			cancel?.Cancel();
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);

		}
	}
}
