namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallHRDPackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);
            await InstallWampVersion(selectedApps);

            if (selectedApps.Contains("PIMS")) await InstallPIMS();
            if (selectedApps.Contains("FSDM")) await InstallFSDM();
        }

    }
}