namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallReceivingPackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);

            if (selectedApps.Contains("Bartender") || selectedApps.Contains("Bartender Drivers")) await InstallBartender();
            if (selectedApps.Contains("MMS (PCOMM)")) await InstallMMS();
            if (selectedApps.Contains("PIMS")) await InstallPIMS();
        }
    }
}