namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallICDPackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);
            if (selectedApps.Contains("PIMS")) await InstallPIMS();
            if (selectedApps.Contains("MMS (PCOMM)")) await InstallMMS();
            if (selectedApps.Contains("Wampserver 3.4.0")) await InstallWampServer();
            if (selectedApps.Contains("Inventory Tools")) await InstallInventoryTools();
            if (selectedApps.Contains("Variance")) await PasteVARIANCE();
            if (selectedApps.Contains(".NET Framework 3.5")) await InstallNetFx3();
        }
    }
}