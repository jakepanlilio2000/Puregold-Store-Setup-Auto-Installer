using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace PGInstaller
{

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            LoginWindow login = new();
            login.Show();
        }
    }

}
