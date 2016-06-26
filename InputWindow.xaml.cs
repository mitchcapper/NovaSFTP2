using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NovaSFTP2 {
	/// <summary>
	/// Interaction logic for InputWindow.xaml
	/// </summary>
	public partial class InputWindow : Window {
		public InputWindow() {
			InitializeComponent();
			Loaded += (sender, args) => txtInput.Focus();
		}

		public static string GetInput(String title, String value) {
			var wind = new InputWindow();
			wind.Title = title;
			wind.txtInput.Text = value;
			
			var res = wind.ShowDialog();
			if (res != true)
				return null;
			return wind.txtInput.Text;
		}

		private void btnOk_Click(object sender, RoutedEventArgs e) {
			DialogResult = true;
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
		}
	}
}
