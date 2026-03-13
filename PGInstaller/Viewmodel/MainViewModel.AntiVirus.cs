using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

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

        [RelayCommand]
        private async Task InstallAntivirus()
        {
            if (string.IsNullOrEmpty(SelectedAntivirus)) return;

            if (AntivirusMap.TryGetValue(SelectedAntivirus, out string? fileName))
            {
                string relativePath = Path.Combine("av", fileName);
                string fullSourcePath = Path.Combine(_assetsPath, "av", fileName);

                if (fileName.EndsWith(".zip"))
                {
                    if (!File.Exists(fullSourcePath))
                    {
                        Log($"   [ERROR] {fileName} not found in Assets/av folder.");
                        return;
                    }
                    string extractDir = @"C:\Assets\AV_Install";

                    if (Directory.Exists(extractDir))
                        try { Directory.Delete(extractDir, true); } catch { }

                    Directory.CreateDirectory(extractDir);

                    Log($"   [EXTRACT] Unzipping {fileName}...");
                    try
                    {
                        await Task.Run(() => ZipFile.ExtractToDirectory(fullSourcePath, extractDir));
                    }
                    catch (Exception ex) { Log($"   [ERROR] Extraction failed: {ex.Message}"); return; }

                    if (SelectedAntivirus.Contains("Avast"))
                    {
                        Log("   [INSTALL] Starting Avast Silent Install...");

                        var cmdFile = Directory.GetFiles(extractDir, "Silent Installing.cmd", SearchOption.AllDirectories).FirstOrDefault();

                        if (cmdFile != null)
                        {
                            string scriptDir = Path.GetDirectoryName(cmdFile)!;

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

                            await RunCustomProcess(startInfo, "Avast Setup");
                        }
                        else
                        {
                            Log("   [ERROR] 'Silent Installing.cmd' not found.");
                        }
                    }
                    else if (SelectedAntivirus.Contains("Symantec"))
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
                    if (fileName.EndsWith(".msi"))
                        await SmartInstall(SelectedAntivirus, relativePath, "/qn /norestart", SelectedAntivirus);
                    else if (fileName.EndsWith(".bat"))
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
    }
}