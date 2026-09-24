using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task<bool> HandleComputerRenameAsync()
        {
            string currentName = Environment.MachineName;
            bool isDefaultName = Regex.IsMatch(currentName, @"^(DESKTOP-[A-Z0-9]{7}|WIN-[A-Z0-9]{4,})$", RegexOptions.IgnoreCase);

            if (!isDefaultName)
            {
                Log($"   [INFO] Computer name is already customized ('{currentName}'). Skipping rename prompt.");
                return true;
            }

            var result = MessageBox.Show(
                $"This computer has a default name: '{currentName}'.\n\nWould you like to rename it now?",
                "Computer Rename",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string newName = await Application.Current.Dispatcher.InvokeAsync(() =>
                    ShowInputDialog("Enter new computer name:", currentName));

                if (!string.IsNullOrWhiteSpace(newName) && !newName.Equals(currentName, StringComparison.OrdinalIgnoreCase))
                {
                    bool renamed = await ExecuteRenameComputerInternal(newName);
                    if (renamed)
                    {
                        var restart = MessageBox.Show(
                            $"Computer has been renamed to '{newName}'.\n\nA restart is required to apply the new computer name.\n\nRestart now?",
                            "Restart Required",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (restart == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "shutdown.exe",
                                Arguments = "/r /t 0",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                            return false; 
                        }
                    }
                }
            }
            return true;
        }

        private async Task<bool> ExecuteRenameComputerInternal(string newName)
        {
            string currentName = Environment.MachineName;
            if (string.IsNullOrWhiteSpace(newName) || newName.Equals(currentName, StringComparison.OrdinalIgnoreCase))
                return true;

            newName = newName.Trim();
            Log($"   [INIT] Renaming computer from '{currentName}' to '{newName}'...");

            bool success = false;
            try
            {
                // 1. Primary: Modern PowerShell Rename-Computer (Reliable on Win10/11)
                string psCmd = $"Rename-Computer -NewName '{newName}' -Force -ErrorAction Stop";
                success = await RunProcessAsync("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCmd}\"", "Renaming Computer (PowerShell)", true);

                // 2. Fallback: Native WMI Win32_ComputerSystem API
                if (!success)
                {
                    Log("   [FALLBACK] Attempting WMI ComputerSystem Rename...");
                    await Task.Run(() =>
                    {
                        try
                        {
                            using var searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem");
                            foreach (ManagementObject mo in searcher.Get())
                            {
                                var inParams = mo.GetMethodParameters("Rename");
                                inParams["Name"] = newName;
                                var outParams = mo.InvokeMethod("Rename", inParams, null);
                                uint retVal = Convert.ToUInt32(outParams?["ReturnValue"] ?? 1);
                                if (retVal == 0)
                                {
                                    success = true;
                                    break;
                                }
                                else
                                {
                                    Log($"   [WARN] WMI Rename returned code: {retVal}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"   [WARN] WMI Rename failed: {ex.Message}");
                        }
                    });
                }

                // 3. Fallback: wmic (for older Windows installations)
                if (!success)
                {
                    success = await RunProcessAsync("wmic", $"computersystem where name=\"{currentName}\" call rename name=\"{newName}\"", "Renaming Computer (WMIC)", true);
                }

                if (success)
                {
                    PcName = newName;
                    Log($"   [SUCCESS] Computer renamed to '{newName}'. (Restart required to apply changes)");
                }
                else
                {
                    Log($"   [ERROR] Failed to rename computer to '{newName}'.");
                }
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Exception while renaming computer: {ex.Message}");
                success = false;
            }

            return success;
        }
    }
}
