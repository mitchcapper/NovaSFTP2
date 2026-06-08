using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using iNKORE.UI.WPF.Modern.Controls;
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
				System.Windows.MessageBox.Show(instance, message, caption);
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

		private void FileCRTPathBox_OnTextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e) {
			UpdatePathSuggestions(sender, "pem", e);
		}
		private void FilePFXPathBox_OnTextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e) {
			UpdatePathSuggestions(sender, "pfx", e);
		}
		private void FilePathBox_OnTextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e) {
			UpdatePathSuggestions(sender, null, e);
		}
		private void PathBox_OnSuggestionChosen(object sender, AutoSuggestBoxSuggestionChosenEventArgs e) {
			if (sender is AutoSuggestBox box && e.SelectedItem is string path)
				box.Text = path;
		}
		private void PathBox_OnQuerySubmitted(object sender, AutoSuggestBoxQuerySubmittedEventArgs e) {
			if (sender is AutoSuggestBox box && e.ChosenSuggestion is string path)
				box.Text = path;
		}
		private static void UpdatePathSuggestions(object sender, string allowed_ext, AutoSuggestBoxTextChangedEventArgs e) {
			if (sender is not AutoSuggestBox box || e.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
				return;

			box.ItemsSource = GetPathCandidates(box.Text, allowed_ext);
		}
		private static List<string> GetPathCandidates(string text, string allowed_ext) {
			var searchText = text?.Trim() ?? String.Empty;
			if (String.IsNullOrWhiteSpace(searchText))
				return new List<string>();

			var isExistingDirectory = Directory.Exists(searchText) && !searchText.EndsWith("\\.", StringComparison.Ordinal);
			var dirname = isExistingDirectory ? searchText : Path.GetDirectoryName(searchText);
			if (String.IsNullOrWhiteSpace(dirname) || !Directory.Exists(dirname))
				return new List<string>();

			var prefix = isExistingDirectory ? EnsureTrailingDirectorySeparator(searchText) : searchText;
			try {
				var candidates = Directory.GetDirectories(dirname, "*", SearchOption.TopDirectoryOnly)
					.Where(path => path.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
					.ToList();
				if (!String.IsNullOrWhiteSpace(allowed_ext)) {
					var files = Directory.GetFiles(dirname, "*." + allowed_ext, SearchOption.TopDirectoryOnly)
						.Where(path => path.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
					candidates.AddRange(files);
				}
			return candidates;
			} catch (Exception) {
				return new List<string>();
			}
		}
		private static string EnsureTrailingDirectorySeparator(string path) {
			return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
				? path
				: path + Path.DirectorySeparatorChar;
		}
	}
}
