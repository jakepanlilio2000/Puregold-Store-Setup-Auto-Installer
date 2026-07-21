using System;
using System.Security;
using System.Threading.Tasks;
using System.Windows;

namespace PGInstaller
{
    public partial class DomainJoinWindow : Window
    {
        public string Domain => TxtDomain.Text.Trim();
        public string Username => TxtUsername.Text.Trim();
        public SecureString Password => TxtPassword.SecurePassword;

        public Func<string, string, SecureString, Task<(bool success, bool rebootRequired, string message)>>? JoinAction { get; set; }

        public DomainJoinWindow()
        {
            InitializeComponent();
            TxtDomain.Text = "puregold.com.ph"; 
        }

        private async void BtnJoin_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Domain) || string.IsNullOrWhiteSpace(Username) || Password.Length == 0)
            {
                MessageBox.Show("Please fill in all fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadingBar.Visibility = Visibility.Visible;
            BtnJoin.IsEnabled = false;
            BtnCancel.IsEnabled = false;

            if (JoinAction != null)
            {
                var result = await JoinAction(Domain, Username, Password);

                if (result.success)
                {
                    MessageBox.Show(result.message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(result.message, "Domain Join Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    LoadingBar.Visibility = Visibility.Collapsed;
                    BtnJoin.IsEnabled = true;
                    BtnCancel.IsEnabled = true;
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}