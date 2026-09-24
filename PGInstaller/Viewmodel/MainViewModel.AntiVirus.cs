using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    public partial class MainViewModel
    {
        [ObservableProperty]
        private string? _selectedAntivirus;

        public Dictionary<string, string> AntivirusMap { get; } = new Dictionary<string, string>
        {
            { "Malwarebytes AdwCleaner", "adwcleaner.exe" },
            { "Symantec Endpoint Protection (Windows Server)", "symantec.zip" },
            { "Avast Premium", "avast.zip" }
        };

        public ObservableCollection<string> AntivirusList => new(AntivirusMap.Keys);

        [RelayCommand(CanExecute = nameof(CanRunTool))]
        private async Task InstallAntivirus()
        {
            if (IsBusy) return;
            if (string.IsNullOrEmpty(SelectedAntivirus)) return;

            IsBusy = true;
            NotifyCommands();

            try
            {
                if (AntivirusMap.TryGetValue(SelectedAntivirus, out string? fileName))
                {
                    string relativePath = Path.Combine("av", fileName);
                    string? fullSourcePath = ResolveAssetPath(relativePath) ?? ResolveAssetPath(fileName);

                    // Lazy on-demand extraction if not found in Assets
                    if (string.IsNullOrEmpty(fullSourcePath) || !File.Exists(fullSourcePath))
                    {
                        await ExtractSpecificFile(null, $"*{fileName}*");
                        fullSourcePath = ResolveAssetPath(relativePath) ?? ResolveAssetPath(fileName);
                    }

                    if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(fullSourcePath) || !File.Exists(fullSourcePath))
                        {
                            Log($"   [ERROR] {fileName} not found in Assets/av folder.");
                            return;
                        }
                        string extractDir = @"C:\Assets\AV_Install";

                        if (Directory.Exists(extractDir))
                        {
                            try { Directory.Delete(extractDir, true); } catch { }
                        }

                        Directory.CreateDirectory(extractDir);

                        Log($"   [EXTRACT] Unzipping {fileName}...");
                        try
                        {
                            await ExtractWithProgress(fullSourcePath, extractDir, CreateStepProgress($"Unzipping {fileName}"));
                        }
                        catch (Exception ex)
                        {
                            Log($"   [ERROR] Extraction failed: {ex.Message}");
                            return;
                        }

                        if (SelectedAntivirus.Contains("Avast", StringComparison.OrdinalIgnoreCase))
                        {
                            Log("   [INSTALL] Starting Avast Premium Silent Install...");

                            var cmdFile = Directory.GetFiles(extractDir, "Silent Installing.cmd", SearchOption.AllDirectories).FirstOrDefault()
                                          ?? Directory.GetFiles(extractDir, "*Silent*Install*.cmd", SearchOption.AllDirectories).FirstOrDefault()
                                          ?? Directory.GetFiles(extractDir, "*Silent*Install*.bat", SearchOption.AllDirectories).FirstOrDefault();

                            if (cmdFile != null)
                            {
                                Log($"   [EXEC] Running '{Path.GetFileName(cmdFile)}'...");
                                string? scriptDir = Path.GetDirectoryName(cmdFile) ?? extractDir;
                                var startInfo = new ProcessStartInfo
                                {
                                    FileName = "cmd.exe",
                                    Arguments = $"/c \"{cmdFile}\"",
                                    WorkingDirectory = scriptDir,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true
                                };
                                await RunCustomProcess(startInfo, "Avast Premium Silent Install");
                            }
                            else
                            {
                                var avastExe = Directory.GetFiles(extractDir, "*Avast*Premium*.exe", SearchOption.AllDirectories).FirstOrDefault()
                                               ?? Directory.GetFiles(extractDir, "Avast.Premium.exe", SearchOption.AllDirectories).FirstOrDefault()
                                               ?? Directory.GetFiles(extractDir, "*Avast*.exe", SearchOption.AllDirectories).FirstOrDefault();

                                if (avastExe != null)
                                {
                                    Log($"   [EXEC] Executing '{Path.GetFileName(avastExe)}' in silent mode...");
                                    await RunProcessAsync(avastExe, "/silent", "Avast Premium Setup");
                                }
                                else
                                {
                                    Log("   [ERROR] 'Silent Installing.cmd' or Avast installer executable not found in extracted archive.");
                                }
                            }
                        }
                        else if (SelectedAntivirus.Contains("Symantec", StringComparison.OrdinalIgnoreCase))
                        {
                            Log("   [INSTALL] Starting Symantec Endpoint Protection Silent Install...");
                            var setupExe = Directory.GetFiles(extractDir, "Setup.exe", SearchOption.AllDirectories).FirstOrDefault();

                            if (setupExe != null)
                            {
                                string args = "/s /v\"/qn /norestart\"";
                                await RunProcessAsync(setupExe, args, "Symantec Endpoint Protection");
                            }
                            else
                            {
                                var msiExe = Directory.GetFiles(extractDir, "Sep64.msi", SearchOption.AllDirectories).FirstOrDefault();
                                if (msiExe != null)
                                {
                                    Log("   [WARN] Setup.exe not found, falling back to Sep64.msi...");
                                    await RunProcessAsync("msiexec.exe", $"/i \"{msiExe}\" /qn /norestart", "Symantec Endpoint Protection (MSI)");
                                }
                                else
                                {
                                    Log("   [ERROR] Setup.exe or Sep64.msi not found in Symantec zip.");
                                }
                            }
                        }

                        Log($"   [SUCCESS] {SelectedAntivirus} installation sequence finished.");
                    }
                    else
                    {
                        if (fileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                            await SmartInstall(SelectedAntivirus, relativePath, "/qn /norestart", SelectedAntivirus);
                        else if (fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                            await RunScriptTask(relativePath, $"Running {SelectedAntivirus}...");
                        else
                            await SmartInstall(SelectedAntivirus, relativePath, "/silent", SelectedAntivirus);
                    }
                }
                else
                {
                    Log($"   [ERROR] Configuration not found for: {SelectedAntivirus}");
                }
            }
            finally
            {
                IsBusy = false;
                NotifyCommands();
            }
        }
    }
}
