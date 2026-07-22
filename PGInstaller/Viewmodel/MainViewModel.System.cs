using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task<bool> HandleComputerRenameAsync()
        {
            if (IsDomainJoined())
            {
                Log("   [INFO] Computer is already domain-joined. Skipping local rename prompt.");
                return true;
            }

            string currentName = Environment.MachineName;
            string newName = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog($"Current Computer Name: {currentName}\n\nEnter new computer name (leave blank to skip):", currentName));

            if (!string.IsNullOrWhiteSpace(newName) && !newName.Equals(currentName, StringComparison.OrdinalIgnoreCase))
            {
                if (newName.Length > 15 || newName.Contains(' ') || newName.Contains('\\') || newName.Contains('/'))
                {
                    MessageBox.Show("Computer name cannot exceed 15 characters and must not contain spaces or special characters (\\ /).", "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                Log($"   [CONFIG] Renaming computer from '{currentName}' to '{newName}'...");

                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                    foreach (ManagementObject computer in searcher.Get().Cast<ManagementObject>())
                    {
                        var result = computer.InvokeMethod("Rename", [newName, null!, null!]);
                        int returnCode = Convert.ToInt32(result);

                        if (returnCode == 0)
                        {
                            Log("   [SUCCESS] Computer renamed successfully. A reboot is required.");
                            var rebootResult = MessageBox.Show("Computer renamed successfully. A restart is required for the new name to take effect.\n\nRestart now?", "Restart Required", MessageBoxButton.YesNo, MessageBoxImage.Information);
                            if (rebootResult == MessageBoxResult.Yes)
                            {
                                Process.Start(new ProcessStartInfo { FileName = "shutdown.exe", Arguments = "/r /t 0 /c \"Restarting to apply computer name\"", UseShellExecute = false });
                                Application.Current.Shutdown();
                                return false; 
                            }
                        }
                        else
                        {
                            Log($"   [ERROR] Failed to rename computer. Return code: {returnCode}");
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Rename failed: {ex.Message}");
                }
            }
            return true;
        }
    }
}
