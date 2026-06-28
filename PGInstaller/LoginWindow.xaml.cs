using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace PGInstaller
{
    public partial class LoginWindow : Window
    {
        private const string AccessHash = "7edeb4a074d0423846fdaaf1126585e65420cf10a4a8ed38bb34165513b7e5c3";

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => TxtPassword.Focus();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e) => ValidatePassword();
        private void BtnExit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ValidatePassword();
        }

        private void ValidatePassword()
        {
            var raw = TxtPassword.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(raw))
            {
                MessageBox.Show("Please enter the access key.", "Authentication", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPassword.Focus();
                return;
            }

            string inputHash = ComputeSha256Hex(raw);

            if (FixedTimeEqualsHex(inputHash, AccessHash))
            {
                var main = new MainWindow();
                Application.Current.MainWindow = main;
                main.Show();
                Close();
                return;
            }

            MessageBox.Show("Access Denied: Invalid Credentials", "Security Alert", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtPassword.Clear();
            TxtPassword.Focus();
        }

        private static string ComputeSha256Hex(string rawData)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private static bool FixedTimeEqualsHex(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}