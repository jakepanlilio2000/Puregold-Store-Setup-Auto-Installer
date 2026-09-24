using System;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PGInstaller
{
    public partial class DomainJoinWindow : Window
    {
        public string Domain => TxtDomain.Text.Trim();
        public string Username => TxtUsername.Text.Trim();
        public string PasswordText => TxtPassword.Password;
        public SecureString Password => TxtPassword.SecurePassword;

        public Func<string, Task<(bool valid, string message)>>? ValidateAction { get; set; }
        public Func<string, string, string, Task<(bool success, bool rebootRequired, string message)>>? JoinAction { get; set; }

        public DomainJoinWindow(string defaultDomain = "")
        {
            InitializeComponent();
            TxtDomain.Text = !string.IsNullOrWhiteSpace(defaultDomain) ? defaultDomain : "puregold.com.ph";

            Loaded += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(TxtDomain.Text))
                    TxtDomain.Focus();
                else
                    TxtUsername.Focus();
            };
        }

        private void SetStatus(string text, bool isError)
        {
            TxtStatus.Text = text;
            TxtStatus.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
            if (isError)
            {
                TxtStatus.Foreground = FindResource("DangerBrush") as Brush ?? Brushes.Crimson;
            }
            else
            {
                TxtStatus.Foreground = FindResource("PrimaryBrush") as Brush ?? Brushes.DodgerBlue;
            }
        }

        private async void BtnJoin_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Domain))
            {
                SetStatus("Please enter the domain name (e.g., PG-SANFERNANDO.COM or corp.local).", true);
                TxtDomain.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                SetStatus("Please enter your domain user account (not admin).", true);
                TxtUsername.Focus();
                return;
            }

            if (string.IsNullOrEmpty(PasswordText))
            {
                SetStatus("Please enter the password for this user account.", true);
                TxtPassword.Focus();
                return;
            }

            LoadingBar.Visibility = Visibility.Visible;
            BtnJoin.IsEnabled = false;
            BtnCancel.IsEnabled = false;

            try
            {
                if (ValidateAction != null)
                {
                    SetStatus($"Checking network connectivity and DNS for '{Domain}'...", false);
                    var valResult = await ValidateAction(Domain);

                    if (!valResult.valid)
                    {
                        SetStatus(valResult.message, true);
                        LoadingBar.Visibility = Visibility.Collapsed;
                        BtnJoin.IsEnabled = true;
                        BtnCancel.IsEnabled = true;

                        var proceed = MessageBox.Show(
                            $"{valResult.message}\n\nDo you want to proceed with domain join anyway?",
                            "Domain Pre-Flight Warning",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (proceed != MessageBoxResult.Yes)
                        {
                            return;
                        }

                        LoadingBar.Visibility = Visibility.Visible;
                        BtnJoin.IsEnabled = false;
                        BtnCancel.IsEnabled = false;
                    }
                }

                SetStatus($"Joining domain '{Domain}'...", false);

                if (JoinAction != null)
                {
                    var result = await JoinAction(Domain, Username, PasswordText);

                    if (result.success)
                    {
                        SetStatus(result.message, false);
                        MessageBox.Show(result.message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        SetStatus(result.message, true);
                        MessageBox.Show(result.message, "Domain Join Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        LoadingBar.Visibility = Visibility.Collapsed;
                        BtnJoin.IsEnabled = true;
                        BtnCancel.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}", true);
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Domain Join Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadingBar.Visibility = Visibility.Collapsed;
                BtnJoin.IsEnabled = true;
                BtnCancel.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}