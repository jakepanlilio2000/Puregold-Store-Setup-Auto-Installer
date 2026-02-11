using System.IO;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallHRDPackage()
        {
            await InstallCommonPackages();
            await InstallPIMS();
            await InstallFSDM();
        }
    }
}