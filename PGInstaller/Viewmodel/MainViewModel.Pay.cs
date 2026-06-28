namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallPayablesPackage()
        {
            await InstallCommonPackages();
            await InstallMMS();
        }
    }
}