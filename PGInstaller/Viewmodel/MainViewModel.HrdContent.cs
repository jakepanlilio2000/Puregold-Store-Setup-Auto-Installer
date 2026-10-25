using System.Diagnostics;
using System.IO;
using System.Management;
using PGInstaller.Services;

namespace PGInstaller.Viewmodel;
partial class MainViewModel
{
    private async Task DeployHrdWampContent()
    {
        if (_packageRestartRequired)
            throw new InvalidOperationException("Restart Windows before applying HRD Wamp content.");
        const string target = @"C:\wamp";
        if (!File.Exists(Path.Combine(target, "wampmanager.exe")))
            throw new DirectoryNotFoundException("Wamp 2 did not install to C:\\wamp. HRD content was not applied.");
        var archive = await EnsureAssetAsync("wampHR.zip") ?? throw new FileNotFoundException("wampHR.zip is missing.");
        var staging = Path.Combine(@"C:\", $"PGInstaller_HRD_{Guid.NewGuid():N}");
        var backup = $"{target}.PGBackup-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var runningServices = new List<string>();
        bool deployed = false;
        try
        {
            await ExtractWithProgress(archive, staging, CreateStepProgress("Preparing HRD Wamp content"));
            var replacement = Path.Combine(staging, "wamp");
            if (!File.Exists(Path.Combine(replacement, "wampmanager.exe")) || !Directory.Exists(Path.Combine(replacement, "www")))
                throw new InvalidDataException("wampHR.zip must contain wamp/wampmanager.exe and wamp/www.");
            using var searcher = new ManagementObjectSearcher("SELECT Name, State, PathName FROM Win32_Service");
            using var services = searcher.Get();
            foreach (ManagementObject service in services)
            {
                using (service)
                {
                    var image = service["PathName"]?.ToString()?.TrimStart('"') ?? "";
                    if (!image.StartsWith(target + "\\", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (service["State"]?.ToString() == "Running")
                    {
                        var name = service["Name"]!.ToString()!;
                        runningServices.Add(name);
                        if (!await RunProcessAsync("sc.exe", $"stop \"{name}\"", $"Stopping {name}"))
                            throw new IOException($"Cannot stop Wamp service {name}.");
                        await WaitForServiceState(name, "Stopped");
                    }
                }
            }

            foreach (var process in Process.GetProcessesByName("wampmanager"))
            {
                using (process)
                {
                    if (process.MainModule?.FileName?.StartsWith(target + "\\", StringComparison.OrdinalIgnoreCase) != true)
                        continue;
                    process.Kill();
                    await process.WaitForExitAsync();
                }
            }

            await DirectoryDeployment.ReplaceAsync(target, replacement, backup, async () =>
            {
                foreach (var name in runningServices)
                {
                    if (!await RunProcessAsync("sc.exe", $"start \"{name}\"", $"Starting {name}"))
                        throw new IOException($"HRD Wamp service {name} failed to start.");
                    await WaitForServiceState(name, "Running");
                }
            }, async () =>
            {
                foreach (var name in runningServices)
                {
                    await RunProcessAsync("sc.exe", $"stop \"{name}\"", $"Stopping {name} for rollback", true);
                    await WaitForServiceState(name, "Stopped");
                }
            });
            deployed = true;
            Log($"   [SUCCESS] HRD Wamp content replaced. Original retained at {backup}");
        }
        finally
        {
            if (!deployed)
            {
                if (Directory.Exists(backup))
                    Log($"   [ERROR] HRD replacement failed. The original Wamp folder is retained at {backup}.");
                else
                    Log("   [WARN] HRD replacement did not complete. The original Wamp folder is in place.");
                foreach (var name in runningServices)
                    await RunProcessAsync("sc.exe", $"start \"{name}\"", $"Restoring {name}", true);
            }

            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
    }

    private static async Task WaitForServiceState(string name, string state)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            using var service = new ManagementObject($"Win32_Service.Name='{name.Replace("'", "\\'")}'");
            service.Get();
            if (service["State"]?.ToString() == state)
                return;
            await Task.Delay(500);
        }

        throw new TimeoutException($"Service {name} did not reach {state} within 30 seconds.");
    }
}
