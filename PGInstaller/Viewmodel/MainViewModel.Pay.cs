namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallPayablesPackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);
            if (selectedApps.Contains("MMS (PCOMM)")) await InstallMMS();
        }
    }
}