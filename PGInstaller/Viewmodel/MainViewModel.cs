using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PGInstaller.Viewmodel
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty] private string? _logOutput;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string? _selectedDepartment;

        private string _assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");

        public ObservableCollection<string> PreviewList { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> Departments { get; } = new ObservableCollection<string>
        {
            "IT", "HRD", "ICD", "Payables", "Creative", "Admin", "Audit",
            "Store Operations (Manager)", "Store Operations (Customer Service)",
            "Store Operations (Gcash)", "Store Operations (HBC)", "Receiving", "Treasury",
        };

        public MainViewModel()
        {
            SelectedDepartment = "IT";
            Log("Welcome to PG Installer. Select a department to begin.");
        }

        [RelayCommand]
        private async Task Install()
        {
            if (IsBusy) return;
            IsBusy = true;
            LogOutput = "";
            Log("------------------------------------------------");
            Log($"Starting Installation for: {SelectedDepartment}");

            try
            {
                await PrepareAssets();

                switch (SelectedDepartment)
                {
                    case "IT": await InstallITPackage(); break;
                    case "HRD": await InstallHRDPackage(); break;
                    case "ICD": await InstallICDPackage(); break;
                    case "Payables": await InstallPayablesPackage(); break;
                    case "Admin": await InstallAdminPackage(); break;
                    case "Audit": await InstallAuditPackage(); break;
                    case "Store Operations (Manager)": await InstallStoreOperationsPackage("Manager"); break;
                    case "Store Operations (Customer Service)": await InstallStoreOperationsPackage("Customer Service"); break;
                    case "Store Operations (Gcash)": await InstallStoreOperationsPackage("Gcash"); break;
                    case "Store Operations (HBC)": await InstallStoreOperationsPackage("HBC"); break;
                    case "Creative": await InstallCreativePackage(); break;
                    case "Receiving": await InstallReceivingPackage(); break;
                    case "Treasury": await InstallTreasuryPackage(); break;
                    default: Log("No specific package defined for this department yet."); break;
                }
            }
            catch (Exception ex)
            {
                Log($"CRITICAL ERROR: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                Log("------------------------------------------------");
                Log("Process Completed.");
            }
        }
        private async Task SmartInstall(string appName, string offlineExe, string offlineArgs = "/silent", string? checkName = null)
        {
            if (!string.IsNullOrEmpty(checkName))
            {
                if (IsAppInstalled(checkName))
                {
                    Log($"   [SKIP] {appName} is already installed.");
                    return;
                }
            }
            string installerPath = Path.Combine(_assetsPath, offlineExe);

            if (File.Exists(installerPath))
            {
                if (offlineExe.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                {
                    string msiArgs = $"/i \"{installerPath}\" {offlineArgs}";
                    await RunProcessAsync("msiexec.exe", msiArgs, $"[OFFLINE] Installing {appName} (MSI)");
                }
                else
                {
                    await RunProcessAsync(installerPath, offlineArgs, $"[OFFLINE] Installing {appName}");
                }
            }
            else
            {
                Log($"   [SKIP] Installer missing: {offlineExe}");
            }
        }
        private bool IsAppInstalled(string partialName)
        {
            string? displayName;
            RegistryKey? key;
            List<string> registryPaths = new List<string>()
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var path in registryPaths)
            {
                try
                {
                    using (key = Registry.LocalMachine.OpenSubKey(path))
                    {
                        if (key != null)
                        {
                            foreach (var subkeyName in key.GetSubKeyNames())
                            {
                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    displayName = subkey?.GetValue("DisplayName") as string;
                                    if (!string.IsNullOrEmpty(displayName) &&
                                        displayName.Contains(partialName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return false;
        }

        private async Task PrepareAssets()
        {
            string localAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");

            if (File.Exists(Path.Combine(localAssets, "Chrome.exe")) || File.Exists(Path.Combine(localAssets, "7z.exe")))
            {
                _assetsPath = localAssets;
                Log($"   [INIT] Using Local Assets folder: {_assetsPath}");
                return;
            }
            string zipFile = Path.Combine(localAssets, "assets.zip");
            if (File.Exists(zipFile))
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "PGInstaller_Assets");
                if (!Directory.Exists(tempRoot) || !File.Exists(Path.Combine(tempRoot, "7z.exe")))
                {
                    Log("   [INFO] Extracting Assets.zip to Temp folder...");
                    try
                    {
                        if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                        Directory.CreateDirectory(tempRoot);
                        await Task.Run(() => ZipFile.ExtractToDirectory(zipFile, tempRoot));
                        Log("   [SUCCESS] Extraction complete.");
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Failed to extract assets: {ex.Message}");
                    }
                }

                string subAssets = Path.Combine(tempRoot, "Assets");
                _assetsPath = Directory.Exists(subAssets) ? subAssets : tempRoot;
                Log($"   [INIT] Using Temp Assets: {_assetsPath}");
            }
            else
            {
                _assetsPath = localAssets;
                Log("   [WARNING] No installers found (Checked Local and Zip).");
            }
        }

        #region Package Implementations

        private async Task InstallCommonPackages()
        {
            await SmartInstall("Google Chrome", "Chrome.exe", "/silent /install", "Google Chrome");
            await SmartInstall("Mozilla Firefox", "Firefox.exe", "-ms -ma", "Mozilla Firefox");
            await SmartInstall("7-Zip", "7z.exe", "/S", "7-Zip");
            await SmartInstall("Notepad++", "npp.exe", "/S", "Notepad++");
            await SmartInstall("Mozilla Thunderbird", "Thunderbird.exe", "-ms -ma", "Thunderbird");
            await SmartInstall("Oracle Java Runtime", "jre.exe", "/s", "Java");
            await SmartInstall("VLC", "vlc.exe", "/S", "VLC media player");
            await SmartInstall("Radmin Viewer", "radminv.msi", "/qn /norestart", "Radmin Viewer");

            string aioPath = Path.Combine(_assetsPath, "vcredist_aio.exe");
            if (File.Exists(aioPath))
            {
                if (IsAppInstalled("Microsoft Visual C++ 2015-2022"))
                {
                    Log("   [SKIP] VC++ Runtimes (2015-2022) are already installed.");
                }
                else
                {
                    await RunProcessAsync(aioPath, "/y", "Installing VC++ Runtimes (2005-2026 AIO)");
                }
            }
            else
            {
                Log("   [SKIP] vcredist_aio.exe not found.");
            }
        }

        #endregion

        #region Helpers
        private async Task<bool> RunProcessAsync(string fileName, string arguments, string description, bool suppressError = false)
        {
            Log($"[{DateTime.Now:HH:mm:ss}] {description}...");
            var tcs = new TaskCompletionSource<bool>();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                },
                EnableRaisingEvents = true,
            };

            void OnDataReceived(object s, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string? cleanLine = CleanLogLine(e.Data);
                    if (!string.IsNullOrEmpty(cleanLine))
                        Log($"   > {cleanLine}");
                }
            }

            process.OutputDataReceived += OnDataReceived;
            process.ErrorDataReceived += OnDataReceived;
            process.Exited += (s, e) =>
            {
                tcs.SetResult(process.ExitCode == 0);
                process.Dispose();
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await tcs.Task;
                return true;
            }
            catch (Exception ex)
            {
                if (!suppressError)
                    Log($"   [FAILED] Could not start {fileName}: {ex.Message}");
                return false;
            }
        }

        private string? CleanLogLine(string line)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line)) return null;
            if (line.StartsWith("[=") || line.StartsWith("=======")) return null;
            if (Regex.IsMatch(line, @"\d+%$")) return null;
            if (line.Contains("Extracting")) return null;
            return line;
        }

        private void Log(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogOutput += $"{message}{Environment.NewLine}";
            });
        }
        #endregion
    }
}