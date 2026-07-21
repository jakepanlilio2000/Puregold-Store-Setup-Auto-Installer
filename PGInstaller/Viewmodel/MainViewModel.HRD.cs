namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallHRDPackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);
            if (selectedApps.Contains("Wamp 1.7.2")) await SmartInstall("Wamp5 1.7.2", "wamp5.exe", "/S", "WampServer");
            if (selectedApps.Contains("PIMS")) await InstallPIMS();
            if (selectedApps.Contains("FSDM")) await InstallFSDM();
        }
    }
}