using System.Diagnostics;
using System.Windows;

namespace NovaSFTP2 {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		App() {
			if (! Debugger.IsAttached)
				DispatcherUnhandledException += App_DispatcherUnhandledException;
		}

		void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
			MessageBox.Show("Unhandled exception of: " + e.Exception.Message);
			e.Handled = true;
		}
	}
}
