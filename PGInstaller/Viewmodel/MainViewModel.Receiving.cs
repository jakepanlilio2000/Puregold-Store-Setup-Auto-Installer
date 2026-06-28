namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallReceivingPackage()
        {
            await InstallCommonPackages();
            await InstallBartender();
            await InstallMMS();
            await InstallPIMS();
        }
    }
}