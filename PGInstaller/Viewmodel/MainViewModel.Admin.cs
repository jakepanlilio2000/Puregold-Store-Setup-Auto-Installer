namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallAdminPackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);
            if (selectedApps.Contains("MMS (PCOMM)")) await InstallMMS();
            if (selectedApps.Contains("PIMS")) await InstallPIMS();
        }
    }
}