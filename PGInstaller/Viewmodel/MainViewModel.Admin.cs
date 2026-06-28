namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallAdminPackage()
        {
            await InstallCommonPackages();
            await InstallMMS();
            await InstallPIMS();
        }
    }
}