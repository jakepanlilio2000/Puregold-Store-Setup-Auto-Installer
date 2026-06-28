namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallAuditPackage()
        {
            await InstallCommonPackages();
            await InstallMMS();
            await InstallPIMS();
        }
    }
}