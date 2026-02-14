using System.Windows;

namespace PGInstaller
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnMainWindowClose;

            var login = new LoginWindow();
            MainWindow = login;
            login.Show();
        }
    }
}
