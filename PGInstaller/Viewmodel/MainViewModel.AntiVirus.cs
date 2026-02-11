using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
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
            { "Sophos", "sophos.zip" },         
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
                    string extractDir = Path.Combine(GlobalTempRoot, $"AV_Install_{DateTime.Now.Ticks}");
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                    Directory.CreateDirectory(extractDir);

                    Log($"   [EXTRACT] Unzipping {fileName}...");
                    try
                    {
                        await Task.Run(() => ZipFile.ExtractToDirectory(fullSourcePath, extractDir));
                    }
                    catch (Exception ex) { Log($"   [ERROR] Extraction failed: {ex.Message}"); return; }
                    if (SelectedAntivirus.Contains("Sophos"))
                    {
                        Log("   [INSTALL] Starting Sophos...");

                        var allFiles = Directory.GetFiles(extractDir, "*.*", SearchOption.TopDirectoryOnly);
                        int step = 1;
                        while (true)
                        {
                            string prefix = $"{step}-";

                            var stepFiles = allFiles
                                .Where(f => Path.GetFileName(f).StartsWith(prefix) &&
                                           (f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)))
                                .OrderBy(f => f)
                                .ToList();

                            if (stepFiles.Count == 0)
                            {
                                
                                if (step == 1) Log("   [WARN] No sophos found in Sophos zip.");
                                break;
                            }

                            Log($"   [STEP {step}] Running {stepFiles.Count} file(s)...");

                            foreach (var file in stepFiles)
                            {
                                string fname = Path.GetFileName(file);
                                if (fname.EndsWith(".msi"))
                                    await RunProcessAsync("msiexec.exe", $"/i \"{file}\" /qn /norestart", $"Installing {fname}");
                                else
                                    await RunProcessAsync(file, "/silent", $"Installing {fname}");
                            }

                           
                            step++;
                        }
                    }
                   
                    else if (SelectedAntivirus.Contains("Avast"))
                    {
                        Log("   [INSTALL] Starting Avast Silent Install...");
                        var cmdFile = Directory.GetFiles(extractDir, "Silent Installing.cmd", SearchOption.AllDirectories).FirstOrDefault();

                        if (cmdFile != null)
                        {
                            await RunProcessAsync("cmd.exe", $"/c \"{cmdFile}\"", "Avast Setup", true);
                        }
                        else
                        {
                            Log("   [ERROR] 'Silent Installing.cmd' not found.");
                        }
                    }

                    Log($"   [SUCCESS] {SelectedAntivirus} installation finished.");
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
