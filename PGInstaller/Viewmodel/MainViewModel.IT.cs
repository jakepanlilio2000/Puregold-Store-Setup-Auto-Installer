namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallITPackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);
            var parallelTasks = new List<Func<Task>>();
            if (selectedApps.Contains("Zoom"))
                parallelTasks.Add(() => SmartInstall("Zoom", "ZoomInstaller.exe", "/silent", "Zoom"));

            if (selectedApps.Contains("Advanced IP Scanner"))
                parallelTasks.Add(() => SmartInstall("Advanced IP Scanner", "ipscanner.exe", checkName: "Advanced IP Scanner"));

            if (selectedApps.Contains("PITK"))
                parallelTasks.Add(() => SmartInstall("PITK", "PITK Setup.exe", "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", "PITK"));
            if (parallelTasks.Any())
            {
                Log("   [PARALLEL] Executing independent IT tasks concurrently...");
                await Task.WhenAll(parallelTasks.Select(t => t()));
                Log("   [PARALLEL] Concurrent IT tasks completed.");
            }
            if (selectedApps.Contains("A&VGW")) await InstallAVGW();
            if (selectedApps.Contains("PuTTY (+ Registry Settings)")) await InstallPutty();
            if (selectedApps.Contains("WinSCP (+ Config)")) await InstallWinSCP();
            if (selectedApps.Contains("Radmin Viewer")) await InstallRadminViewer();
            if (selectedApps.Contains("MMS (PCOMM)")) await InstallMMS();
            if (selectedApps.Contains("PIMS")) await InstallPIMS();
            if (selectedApps.Contains("Chrome Bookmarks (CBM)")) await RunChromeBookmarkScript();
        }
    }
}