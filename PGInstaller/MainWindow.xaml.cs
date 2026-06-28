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