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

            MessageBox.Show("Access Denied: Invalid Credentials", "Security Alert",
                MessageBoxButton.OK, MessageBoxImage.Error);

            TxtPassword.Clear();
            TxtPassword.Focus();
        }

        private static string ComputeSha256Hex(string rawData)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static bool FixedTimeEqualsHex(string a, string b)
        {
            try
            {
                var ba = Convert.FromHexString(a);
                var bb = Convert.FromHexString(b);
                return CryptographicOperations.FixedTimeEquals(ba, bb);
            }
            catch
            {
                return false;
            }
        }
    }
}
