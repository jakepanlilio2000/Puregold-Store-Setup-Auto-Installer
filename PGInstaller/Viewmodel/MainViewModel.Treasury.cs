namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallTreasuryPackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);
            if (selectedApps.Contains("MMS (PCOMM)")) await InstallMMS();
        }
    }
}