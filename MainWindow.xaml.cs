using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using NovaSFTP2.Model;
using NovaSFTP2.ViewModel;
using Path = System.IO.Path;

namespace NovaSFTP2 {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		public MainWindow() {
			InitializeComponent();
			vm.dispatcher = Dispatcher;
			Loaded += MainWindow_Loaded;
			instance = this;
			Closing += MainWindow_Closing;
		}

		private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			((DockerFileUploader)vm.uploaders[UPLOADER_TYPE.DOCKER]).Dispose();
		}

		private static MainWindow instance;
		public static void ShowMessage(string message, string caption) {
			instance.Dispatcher.Invoke(() => {
				instance.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
				instance.TaskbarItemInfo.ProgressValue = 100;
				MessageBox.Show(instance, message, caption);
				instance.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
			});
		}

		void MainWindow_Loaded(object sender, RoutedEventArgs e) {
			TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
			vm.ProgressMade += ProgressMade;
			vm.loaded();
		}
		private MainViewModel vm => DataContext as MainViewModel;
		private Task delay_task;
		private async void ThreadedProgMade(double d) {
			TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
			TaskbarItemInfo.ProgressValue = d;
			if (d > 0.99) {
				var tsk = Task.Delay(2000);
				delay_task = tsk;
				await tsk;
				if (tsk == delay_task)
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
			} else {
				delay_task = null;
			}
		}
		private void ProgressMade(object sender, double d) {
			Dispatcher.BeginInvoke((Action)(() => ThreadedProgMade(d)));
		}

		private void FileCRTPathBox_OnPopulating(object sender, PopulatingEventArgs e) {
			FilePathBox_OnPopulating(txtUser, "pem", sender, e);
		}
		private void FilePFXPathBox_OnPopulating(object sender, PopulatingEventArgs e) {
			FilePathBox_OnPopulating(txtPassword, "pfx", sender, e);
		}
		private void FilePathBox_OnPopulating(object sender, PopulatingEventArgs e) {
			FilePathBox_OnPopulating(txtPath, null, sender, e);
		}

		private void FilePathBox_OnPopulating(AutoCompleteBox box, string allowed_ext, object sender, PopulatingEventArgs e) {
			string text = box.Text;
			string dirname = Path.GetDirectoryName(text);
			if (Directory.Exists(text) && !text.EndsWith("\\."))
				dirname = text;
			var candidates = new List<string>();
			if (!String.IsNullOrWhiteSpace(dirname)) {
				try {
					if (Directory.Exists(dirname) || Directory.Exists(Path.GetDirectoryName(dirname))) {
						string[] dirs = Directory.GetDirectories(dirname, "*.*", SearchOption.TopDirectoryOnly);


						Array.ForEach(new[] { dirs }, (x) =>
							Array.ForEach(x, (y) => {
								if (y.StartsWith(dirname, StringComparison.CurrentCultureIgnoreCase))
									candidates.Add(y);
							}));
						if (!String.IsNullOrWhiteSpace(allowed_ext)) {
							var files = Directory.GetFiles(dirname, "*." + allowed_ext, SearchOption.TopDirectoryOnly);
							Array.ForEach(new[] { files }, (x) =>
								Array.ForEach(x, (y) => {
									if (y.StartsWith(dirname, StringComparison.CurrentCultureIgnoreCase))
										candidates.Add(y);
								}));
						}
					}
				} catch (Exception) { }
			}
			box.ItemsSource = candidates;
			box.PopulateComplete();

		}
	}
}
