using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace NovaSFTP2 {


	public static class PasswordBoxHelper {
		public static readonly DependencyProperty BoundPasswordProperty =
			DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper),
				new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

		public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);
		public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

		private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			if (d is PasswordBox passwordBox) {
				// Remove handler to avoid an infinite layout loop
				passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;

				string newPassword = (string)e.NewValue;
				if (passwordBox.Password != newPassword) {
					passwordBox.Password = newPassword;
				}

				passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
			}
		}

		public static readonly DependencyProperty BindPasswordProperty =
			DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordBoxHelper),
				new PropertyMetadata(false, OnBindPasswordChanged));

		public static bool GetBindPassword(DependencyObject d) => (bool)d.GetValue(BindPasswordProperty);
		public static void SetBindPassword(DependencyObject d, bool value) => d.SetValue(BindPasswordProperty, value);

		private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			if (d is PasswordBox passwordBox) {
				if ((bool)e.NewValue) {
					passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
				} else {
					passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
				}
			}
		}

		private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
			if (sender is PasswordBox passwordBox) {
				SetBoundPassword(passwordBox, passwordBox.Password);
			}
		}
	}
}
