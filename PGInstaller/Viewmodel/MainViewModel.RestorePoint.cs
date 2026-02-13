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
            Log("   [LAUNCH] Opening System Restore...");

            if (TryStart("rstrui.exe"))
            {
                Log("   [OK] System Restore Wizard opened.");
                return;
            }

            if (TryStart("SystemPropertiesProtection.exe"))
            {
                Log("   [FALLBACK] Opened System Protection (click 'System Restore...').");
                return;
            }
            if (TryStart("control.exe", "/name Microsoft.System"))
            {
                Log("   [FALLBACK] Opened Control Panel System (go to System Protection).");
                return;
            }
            if (TryStart("ms-settings:recovery"))
            {
                Log("   [FALLBACK] Opened Settings > Recovery (use Advanced startup if needed).");
                return;
            }

            Log("   [ERROR] Could not open System Restore via any known entry point.");
        }

        private bool TryStart(string fileName, string? args = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = true
                };

                if (!string.IsNullOrWhiteSpace(args))
                    psi.Arguments = args;

                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                Log($"   [WARN] Launch failed: {fileName} {(args ?? "")} -> {ex.Message}");
                return false;
            }
        }


    }
}
