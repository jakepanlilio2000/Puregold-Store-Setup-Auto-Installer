using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        public ObservableCollection<BloatwareItem> BloatwareList { get; } = [];

        [ObservableProperty]
        private bool _isScanningBloatware;

        [RelayCommand]
        private async Task ScanBloatware()
        {
            if (IsBusy) return;
            IsBusy = true;
            IsScanningBloatware = true;
            BloatwareList.Clear();
            Log("   [SCAN] Scanning for installed applications...");

            var items = await Task.Run(() =>
            {
                var results = new List<BloatwareItem>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string[] bloatKeywords = [
                    "candy crush", "bubble witch", "spotify", "twitter", "facebook",
                    "bing weather", "bing news", "xbox", "minecraft", "disney", "hulu",
                    "netflix", "tiktok", "instagram", "mcafee", "norton", "avast", "avg",
                    "king.com", "flipboard", "pandora", "solitaire", "zune", "messenger"
                ];

                string[] criticalPublishers = [
                    "microsoft corporation", "intel", "amd", "nvidia", "realtek",
                    "puregold", "jake ashley", "windows", "hp", "dell", "lenovo"
                ];

                void ScanRegistry(RegistryKey baseKey, string path)
                {
                    using var key = baseKey.OpenSubKey(path);
                    if (key == null) return;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName") as string;
                        var uninstallString = subKey.GetValue("UninstallString") as string;
                        var publisher = subKey.GetValue("Publisher") as string;
                        var systemComponent = subKey.GetValue("SystemComponent");

                        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(uninstallString)) continue;
                        if (systemComponent is int sysComp && sysComp == 1) continue;
                        if (seen.Contains(displayName)) continue;

                        if (!string.IsNullOrEmpty(publisher) && criticalPublishers.Any(p => publisher.Contains(p, StringComparison.OrdinalIgnoreCase))) continue;
                        if (displayName.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)) continue;
                        if (displayName.Contains("Update for", StringComparison.OrdinalIgnoreCase)) continue;

                        bool isBloat = bloatKeywords.Any(b => displayName.Contains(b, StringComparison.OrdinalIgnoreCase));

                        results.Add(new BloatwareItem
                        {
                            DisplayName = displayName,
                            UninstallString = uninstallString,
                            IsUwp = false,
                            IsKnownBloat = isBloat,
                            IsSelected = isBloat
                        });
                        seen.Add(displayName);
                    }
                }

                ScanRegistry(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                ScanRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
                ScanRegistry(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"Get-AppxPackage | Select-Object Name, PackageFullName | ConvertTo-Csv -NoTypeInformation\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();

                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 1; i < lines.Length; i++)
                        {
                            var parts = lines[i].Split(',');
                            if (parts.Length >= 2)
                            {
                                string name = parts[0].Trim('"');
                                string pkgName = parts[1].Trim('"');

                                if (bloatKeywords.Any(b => name.Contains(b, StringComparison.OrdinalIgnoreCase) || pkgName.Contains(b, StringComparison.OrdinalIgnoreCase)))
                                {
                                    results.Add(new BloatwareItem
                                    {
                                        DisplayName = $"[UWP] {name}",
                                        UninstallString = pkgName,
                                        IsUwp = true,
                                        IsKnownBloat = true,
                                        IsSelected = true
                                    });
                                }
                            }
                        }
                    }
                }
                catch { }

                return results.OrderBy(x => !x.IsKnownBloat).ThenBy(x => x.DisplayName).ToList();
            });

            foreach (var item in items)
            {
                BloatwareList.Add(item);
            }

            Log($"   [SUCCESS] Scan complete. Found {BloatwareList.Count} applications. Known bloatware auto-selected.");
            IsScanningBloatware = false;
            IsBusy = false;
        }

        [RelayCommand]
        private async Task UninstallSelectedBloatware()
        {
            var selected = BloatwareList.Where(x => x.IsSelected).ToList();
            if (!selected.Any())
            {
                Log("   [INFO] No applications selected for uninstallation.");
                return;
            }

            var confirm = MessageBox.Show(
                $"You are about to forcefully uninstall {selected.Count} application(s).\n\nThis action cannot be easily undone. Proceed?",
                "Confirm Uninstallation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            Log("------------------------------------------------");
            Log($"Starting Aggressive Uninstallation of {selected.Count} items...");

            foreach (var item in selected.ToList())
            {
                try
                {
                    bool success = false;

                    if (item.IsUwp)
                    {
                        Log($"   [UNINSTALL] Forcefully removing UWP App: {item.DisplayName}...");

                        // Extract short name (e.g., "Microsoft.XboxGameCallableUI")
                        string shortName = item.UninstallString.Split('_')[0];

                        // Corrected PowerShell script with proper parameter handling
                        string script = $@"
                        $shortName = '{shortName}'
                        $actionTaken = $false

                        try {{
                            # Tier 1: Remove for ALL users
                            $pkg = Get-AppxPackage -AllUsers -Name $shortName
                            if ($pkg) {{
                                Remove-AppxPackage -Package $pkg.PackageFullName -AllUsers -ErrorAction SilentlyContinue
                                $actionTaken = $true
                            }}

                            # Tier 2: Remove from provisioned packages (Correctly uses PackageName, not PackageFullName)
                            $provPkg = Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '$shortName' }}
                            if ($provPkg) {{
                                Remove-AppxProvisionedPackage -Online -PackageName $provPkg.PackageName -ErrorAction SilentlyContinue
                                $actionTaken = $true
                            }}

                            if ($actionTaken) {{ Write-Host 'SUCCESS' }} else {{ Write-Host 'NOT_FOUND' }}
                        }} catch {{
                            Write-Host ""FAILED: $_""
                        }}
                        ";

                        success = await RunProcessAsync("powershell", $"-NoProfile -Command \"{script}\"", $"Force Removing {item.DisplayName}", false);

                        // Verification Step: Double-check if it's actually gone
                        if (success)
                        {
                            string verifyScript = $"if (Get-AppxPackage -AllUsers -Name '{shortName}') {{ Write-Host 'STILL_EXISTS' }} else {{ Write-Host 'GONE' }}";
                            string verifyResult = await RunProcessCaptureAsync("powershell", $"-NoProfile -Command \"{verifyScript}\"");

                            if (verifyResult.Contains("GONE"))
                            {
                                BloatwareList.Remove(item);
                                Log($"   [SUCCESS] Completely removed: {item.DisplayName}");
                            }
                            else
                            {
                                Log($"   [WARN] Removal command executed, but Windows Resource Protection is actively blocking '{item.DisplayName}'. It is a protected inbox component.");
                            }
                        }
                    }
                    else
                    {
                        Log($"   [UNINSTALL] Removing Win32 App: {item.DisplayName}...");

                        string fileName = "cmd.exe";
                        string args = $"/c \"{item.UninstallString}\" /S /silent /quiet /qn /VERYSILENT /NORESTART";

                        if (item.UninstallString.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
                        {
                            fileName = "msiexec.exe";
                            int idx = item.UninstallString.IndexOf("msiexec.exe", StringComparison.OrdinalIgnoreCase) + "msiexec.exe".Length;
                            args = item.UninstallString.Substring(idx).Trim() + " /qn /norestart";
                        }
                        else if (item.UninstallString.Trim().StartsWith("\""))
                        {
                            int secondQuote = item.UninstallString.IndexOf('"', 1);
                            if (secondQuote > 0)
                            {
                                fileName = item.UninstallString.Substring(1, secondQuote - 1);
                                string existingArgs = item.UninstallString.Substring(secondQuote + 1).Trim();
                                args = $"{existingArgs} /S /silent /quiet /qn /VERYSILENT /NORESTART";
                            }
                        }
                        else
                        {
                            int firstSpace = item.UninstallString.IndexOf(' ');
                            if (firstSpace > 0)
                            {
                                fileName = item.UninstallString.Substring(0, firstSpace);
                                string existingArgs = item.UninstallString.Substring(firstSpace + 1).Trim();
                                args = $"{existingArgs} /S /silent /quiet /qn /VERYSILENT /NORESTART";
                            }
                            else
                            {
                                fileName = item.UninstallString;
                                args = "/S /silent /quiet /qn /VERYSILENT /NORESTART";
                            }
                        }

                        success = await RunProcessAsync(fileName, args, $"Uninstalling {item.DisplayName}", false);

                        if (success)
                        {
                            BloatwareList.Remove(item);
                            Log($"   [SUCCESS] Removed: {item.DisplayName}");
                        }
                        else
                        {
                            Log($"   [WARN] Failed to remove: {item.DisplayName}. Check terminal output for details.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Exception during uninstall of {item.DisplayName}: {ex.Message}");
                }
            }

            Log("------------------------------------------------");
            Log("Uninstallation process completed.");
            IsBusy = false;
        }
    }
}