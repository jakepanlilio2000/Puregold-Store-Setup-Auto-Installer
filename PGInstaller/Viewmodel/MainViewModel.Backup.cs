using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PGInstaller.Viewmodel
{
    public partial class MainViewModel
    {
        [RelayCommand]
        private async Task RunBackup()
        {
            string inputPath = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter Backup Drive (e.g. D:) or Network Share:", @"D:"));

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                Log("   [INFO] Backup cancelled.");
                return;
            }
            string finalTarget = inputPath;
            if (Path.IsPathRooted(inputPath) && !inputPath.StartsWith(@"\\"))
            {
                string? root = Path.GetPathRoot(inputPath);
                if (!string.IsNullOrEmpty(root))
                {
                    finalTarget = root.TrimEnd('\\');

                    if (!inputPath.Equals(finalTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"   [INFO] Subfolders not supported by Windows Backup. Targeting root: {finalTarget}");
                    }
                }
            }

            Log($"   [INIT] Starting System Backup to {finalTarget}...");
            Log("   [NOTE] This will create a 'WindowsImageBackup' folder on the target.");

            await Task.Run(async () =>
            {
                string script = $@"
$Target = '{finalTarget}'
Write-Host ""Targeting: $Target""

# Check if target exists
if ((Test-Path $Target) -or ($Target -match '^\\\\')) {{
    wbadmin start backup -backupTarget:$Target -include:C: -allCritical -quiet
    if (!$?) {{ Write-Error 'Backup process returned an error code.' }}
}}
else {{
    Write-Error ""Target drive/path '$Target' does not exist.""
}}
";
                await RunProcessAsync("powershell", $"-Command \"{script}\"", "System Backup Operation", true);
            });
        }

        [RelayCommand]
        private void RunImageRecovery()
        {
            var result = MessageBox.Show(
                "Restoring a full System Image requires rebooting into Recovery Mode.\n\n" +
                "1. Click 'Yes' to restart into Advanced Startup.\n" +
                "2. Select 'Troubleshoot' > 'Advanced Options'.\n" +
                "3. Select 'System Image Recovery'.\n\n" +
                "Do you want to reboot now?",
                "System Image Recovery",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Log("   [REBOOT] Restarting into Advanced Startup options...");
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/r /o /f /t 00", 
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to initiate reboot: {ex.Message}");
                }
            }
        }
    }
}
