using System.Windows;

namespace PGInstaller
{
    public partial class InputDialog : Window
    {
        public string Answer => TxtAnswer.Text;

        public InputDialog(string question, string defaultAnswer = "")
        {
            InitializeComponent();
            TxtQuestion.Text = question;
            TxtAnswer.Text = defaultAnswer;

            Loaded += (_, _) =>
            {
                TxtAnswer.Focus();
                TxtAnswer.SelectAll();
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}