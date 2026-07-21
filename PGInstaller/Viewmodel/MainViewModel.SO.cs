namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallStoreOperationsPackage(string role, IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);

            var parallelTasks = new List<Func<Task>>();

            if (selectedApps.Contains("VLC Media Player"))
                parallelTasks.Add(() => SmartInstall("VLC Media Player", "vlc.exe", "/S", "VLC"));

            if (role == "Manager" && selectedApps.Contains("Zoom"))
                parallelTasks.Add(() => SmartInstall("Zoom", "ZoomInstaller.exe", "/silent", "Zoom"));

            if (parallelTasks.Any())
            {
                Log("   [PARALLEL] Executing independent Store Operations tasks concurrently...");
                await Task.WhenAll(parallelTasks.Select(t => t()));
                Log("   [PARALLEL] Concurrent Store Operations tasks completed.");
            }

            if (selectedApps.Contains("MMS (PCOMM)")) await InstallMMS();
            if (selectedApps.Contains("PIMS")) await InstallPIMS(); 

            switch (role)
            {
                case "Manager":
                    break;
                case "Customer Service":
                    if (selectedApps.Contains("Bartender")) await InstallBartender();
                    break;
                case "Selling":
                    if (selectedApps.Contains("Bartender")) await InstallBartender();
                    break;
                case "HBC":
                    break;
            }

            Log($"   [SUCCESS] Store Operations ({role}) setup complete.");
        }
    }
}