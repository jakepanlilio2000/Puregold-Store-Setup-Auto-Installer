using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallHRDPackage()
        {
            await InstallCommonPackages();
            await SmartInstall("Wamp5 1.7.2", "wamp5.exe", "/S", "WampServer");
            await InstallPIMS();
            await InstallFSDM();
        }
    }
}