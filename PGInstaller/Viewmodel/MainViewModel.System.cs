using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task HandleComputerRenameAsync()
        {
            string currentName = Environment.MachineName;

            string newName = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog($"Current Computer Name: {currentName}\n\nEnter new computer name (leave blank to skip):", currentName));

            if (!string.IsNullOrWhiteSpace(newName) && !newName.Equals(currentName, StringComparison.OrdinalIgnoreCase))
            {
                if (newName.Length > 15 || newName.Contains(" ") || newName.Contains("\\") || newName.Contains("/"))
                {
                    MessageBox.Show("Computer name cannot exceed 15 characters and must not contain spaces or special characters (\\ /).", "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Log($"   [CONFIG] Renaming computer from '{currentName}' to '{newName}'...");
                string script = $"Rename-Computer -NewName '{newName}' -Force -Restart:$false";
                bool success = await RunProcessAsync("powershell", $"-NoProfile -Command \"{script}\"", "Renaming Computer", true);

                if (success)
                {
                    Log("   [SUCCESS] Computer renamed successfully. A reboot will be required to apply changes.");
                }
                else
                {
                    Log("   [ERROR] Failed to rename computer. Check administrator privileges.");
                }
            }
        }
    }
}
