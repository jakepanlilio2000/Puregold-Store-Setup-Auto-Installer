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
            TxtPassword.Focus();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ValidatePassword();
            }
        }

        private void ValidatePassword()
        {
            string inputHash = ComputeSha256Hash(TxtPassword.Password);

            if (inputHash == AccessHash)
            {
                MainWindow main = new();
                main.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Access Denied: Invalid Credentials", "Security Alert", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtPassword.Clear();
                TxtPassword.Focus();
            }
        }

        private static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}