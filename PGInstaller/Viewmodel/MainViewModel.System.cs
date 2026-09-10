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

                if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
                {
                    Log($"   [INIT] Renaming computer to '{newName}'...");
                    try
                    {
                        await RunProcessAsync("wmic", $"computersystem where name=\"{currentName}\" call rename name=\"{newName}\"", "Renaming Computer", true);

                        var restart = MessageBox.Show(
                            "A restart is required to apply the new computer name.\n\nRestart now?",
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
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Failed to rename computer: {ex.Message}");
                    }
                }
            }
            return true;
        }
    }
}
