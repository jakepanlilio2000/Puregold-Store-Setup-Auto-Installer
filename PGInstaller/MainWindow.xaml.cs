using System.Text;
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

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            (sender as TextBox)?.ScrollToEnd();
        }

    }
}