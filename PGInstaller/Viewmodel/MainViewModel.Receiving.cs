namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallReceivingPackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);

            if (selectedApps.Any(a => a.Contains("Bartender ") && !a.Contains("Driver")))
            {
                await InstallBartender(selectedApps);
            }

            if (selectedApps.Contains("Bartender Drivers") ||
                selectedApps.Contains("Argox Driver") ||
                selectedApps.Contains("Zebra Driver"))
            {
                await InstallBartenderDrivers(selectedApps);
            }

            if (selectedApps.Contains("MMS (PCOMM)")) await InstallMMS();
            if (selectedApps.Contains("PIMS")) await InstallPIMS();
        }

    }
}