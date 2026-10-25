using PGInstaller.Viewmodel;
using System.Windows;
using System.Windows.Controls;

namespace PGInstaller
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Terminal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel { IsBusy: true })
            {
                e.Cancel = true;
                MessageBox.Show("Wait for the current operation to finish before closing setup.", "Setup is running", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            if (DataContext is MainViewModel vm)
            {
                vm.CleanupSession();
            }
        }
    }
}
