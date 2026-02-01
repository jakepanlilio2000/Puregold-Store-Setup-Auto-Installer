using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    public partial class MainViewModel
    {
        [ObservableProperty]
        private bool _isRestorePointEnabled = true;

        private async Task CreateSystemRestorePoint()
        {
            Log("   [BACKUP] Creating System Restore Point...");
            try
            {
                
                string cmd = "Checkpoint-Computer -Description \"PG-Installer Auto Point\" -RestorePointType \"MODIFY_SETTINGS\"";
                string script = $"try {{ Enable-ComputerRestore -Drive \"C:\"; {cmd} }} catch {{ Write-Error $_ }}";

                await RunProcessAsync("powershell", $"-Command \"{script}\"", "Restore Point Creation", true);
            }
            catch (Exception ex)
            {
                Log($"   [WARN] Failed to create restore point: {ex.Message}");
            }
        }

        private async Task CheckSystemRestoreStatus()
        {
            await Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore");
                    if (key != null)
                    {
                        object? val = key.GetValue("DisableSR");
                        if (val != null && (int)val == 1)
                        {
                            IsRestorePointEnabled = false;
                            Log("   [INFO] System Restore is disabled in Windows settings.");
                        }
                        else
                        {
                            IsRestorePointEnabled = true;
                            Log("   [INFO] System Restore is ready.");
                        }
                    }
                }
                catch
                {
                   
                    IsRestorePointEnabled = false;
                }
            });
        }

        [RelayCommand]
        private void RunSystemRestore()
        {
            try
            {
                Log("   [LAUNCH] Opening System Restore Wizard...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rstrui.exe",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Failed to launch System Restore: {ex.Message}");
            }
        }

    }
}
