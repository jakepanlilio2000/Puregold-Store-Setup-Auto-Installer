using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

namespace PGInstaller.Viewmodel
{
    public partial class MainViewModel : ObservableObject
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [ObservableProperty] private string? _logOutput;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string? _selectedDepartment;
        [ObservableProperty] private int _pendingTasksCount;
        [ObservableProperty]
        private string _domainStatus = "Checking...";
        [ObservableProperty]
        private string _currentTaskDescription = "Ready";
        [ObservableProperty]
        private int _installSuccessCount;
        [ObservableProperty] private string _pcName = Environment.MachineName;
        [ObservableProperty] private string _cpuInfo = "Loading...";
        [ObservableProperty] private string _ramInfo = "Loading...";
        [ObservableProperty] private string _osVersion = "Loading...";
        [ObservableProperty]
        private int _installFailCount;

        [ObservableProperty]
        private int _installSkipCount;
        [ObservableProperty]
        private int _progressPercentage;

        [ObservableProperty]
        private int _totalSteps;

        [ObservableProperty]
        private int _currentStep;
        [ObservableProperty]
        private string _manifestSearchText = "";

        public ICollectionView FilteredPreviewList { get; private set; }
        public ObservableCollection<InstallAppItem> PreviewList { get; } = [];

        private string? _assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");

        public ObservableCollection<string> Departments { get; } =
        [
            "IT",
            "HRD",
            "ICD",
            "Payables",
            "Creative",
            "Admin",
            "Audit",
            "Store Operations (Manager)",
            "Store Operations (Customer Service)",
            "Store Operations (Selling)",
            "Store Operations (HBC)",
            "Receiving",
            "Treasury",
        ];

        public MainViewModel()
        {
            SelectedDepartment = "IT";

            PreviewList.CollectionChanged += PreviewList_CollectionChanged;

            Log("Welcome to PG Installer. Select a department to begin.");
            _ = CheckDefender();
            _ = CheckSystemRestoreStatus();
            _ = CheckDomainStatusAsync();
            _ = LoadSystemInfoAsync();

            FilteredPreviewList = CollectionViewSource.GetDefaultView(PreviewList);
            FilteredPreviewList.Filter = item =>
            {
                if (item is InstallAppItem app)
                {
                    return string.IsNullOrWhiteSpace(ManifestSearchText) ||
                           app.Name.Contains(ManifestSearchText, StringComparison.OrdinalIgnoreCase);
                }
                return true;
            };
        }

        public void IncrementProgress()
        {
            CurrentStep++;
            ProgressPercentage = TotalSteps > 0 ? Math.Min(100, (int)((double)CurrentStep / TotalSteps * 100)) : 0;
        }
        private async Task LoadSystemInfoAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                    foreach (var obj in searcher.Get()) { CpuInfo = obj["Name"]?.ToString()?.Replace("  ", " ").Trim() ?? "Unknown CPU"; break; }

                    using var ramSearcher = new System.Management.ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                    ulong totalCapacity = 0;
                    foreach (var obj in ramSearcher.Get()) { totalCapacity += Convert.ToUInt64(obj["Capacity"]); }
                    RamInfo = $"{Math.Round(totalCapacity / (1024.0 * 1024.0 * 1024.0), 1)} GB RAM";

                    OsVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
                }
                catch { OsVersion = Environment.OSVersion.ToString(); }
            });
        }
        partial void OnManifestSearchTextChanged(string value)
        {
            FilteredPreviewList.Refresh();
        }

        private void ApplySystemOptimizations()
        {
            Log("   [OPTIMIZE] Applying Windows performance and UI optimizations...");
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"))
                    key?.SetValue("", "", RegistryValueKind.String);
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                    key?.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    key?.SetValue("EnableTransparency", 0, RegistryValueKind.DWord);

                Log("   [SUCCESS] System optimizations applied.");
            }
            catch (Exception ex) { Log($"   [WARN] Optimization failed: {ex.Message}"); }
        }

        [RelayCommand]
        private void ClearSearch()
        {
            ManifestSearchText = "";
        }
        private void PreviewList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (InstallAppItem item in e.OldItems)
                    item.PropertyChanged -= Item_PropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (InstallAppItem item in e.NewItems)
                    item.PropertyChanged += Item_PropertyChanged;
            }
            UpdatePendingTasksCount();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InstallAppItem.IsChecked))
            {
                UpdatePendingTasksCount();
            }
        }

        private void UpdatePendingTasksCount()
        {
            PendingTasksCount = PreviewList.Count(x => x.IsChecked);
        }
        private async Task CheckDomainStatusAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
                    if (!string.IsNullOrEmpty(properties.DomainName) && properties.DomainName != properties.HostName)
                    {
                        DomainStatus = $"Joined: {properties.DomainName}";
                    }
                    else
                    {
                        DomainStatus = "Workgroup / Not Joined";
                    }
                }
                catch
                {
                    DomainStatus = "Unknown";
                }
            });
        }
        
        [RelayCommand]
        private async Task Install()
        {
            if (IsBusy) return;

            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            {
                MessageBox.Show("Administrator privileges required.", "Admin Required", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selectedApps = PreviewList.Where(x => x.IsChecked).Select(x => x.Name).ToList();
            if (selectedApps.Count == 0)
            {
                Log("   [WARN] No applications selected for installation.");
                return;
            }

            TotalSteps = PreviewList.Count;
            CurrentStep = 0;
            ProgressPercentage = 0;
            CurrentTaskDescription = "Initializing installation...";

            if (IsRestorePointEnabled)
            {
                IsBusy = true;
                await CreateSystemRestorePoint();
                IsBusy = false;
            }

            IsBusy = true;
            LogOutput = "";
            Log("------------------------------------------------");
            Log($"Starting Installation for: {SelectedDepartment} ({selectedApps.Count} items selected)");

            try
            {

                bool renameProceed = await HandleComputerRenameAsync();
                if (!renameProceed) return;

                bool canProceed = await HandleDomainJoinAsync();
                if (!canProceed)
                {
                    Log("   [INFO] Installation aborted or pending restart. Please run again after restart.");
                    return;
                }

                ApplySystemOptimizations();

                Log("   [CONFIG] Disabling Windows Firewall...");
                await RunProcessAsync("netsh", "advfirewall set allprofiles state off", "Disabling Windows Firewall", true);

                bool assetsReady = await PrepareAssets();
                if (!assetsReady)
                {
                    Log("CRITICAL: Failed to prepare assets. Stopping.");
                    return;
                }

                switch (SelectedDepartment)
                {
                    case "IT": await InstallITPackage(selectedApps); break;
                    case "HRD": await InstallHRDPackage(selectedApps); break;
                    case "ICD": await InstallICDPackage(selectedApps); break;
                    case "Payables": await InstallPayablesPackage(selectedApps); break;
                    case "Admin": await InstallAdminPackage(selectedApps); break;
                    case "Audit": await InstallAuditPackage(selectedApps); break;
                    case "Store Operations (Manager)": await InstallStoreOperationsPackage("Manager", selectedApps); break;
                    case "Store Operations (Customer Service)": await InstallStoreOperationsPackage("Customer Service", selectedApps); break;
                    case "Store Operations (Selling)": await InstallStoreOperationsPackage("Selling", selectedApps); break;
                    case "Store Operations (HBC)": await InstallStoreOperationsPackage("HBC", selectedApps); break;
                    case "Creative": await InstallCreativePackage(selectedApps); break;
                    case "Receiving": await InstallReceivingPackage(selectedApps); break;
                    case "Treasury": await InstallTreasuryPackage(selectedApps); break;
                    default:
                        Log("No specific package defined for this department yet.");
                        break;
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
                Application.Current.Dispatcher.Invoke(ShowInstallationSummary);
            }
        }


        private async Task SmartInstall(
    string appName,
    string exeName,
    string args = "/silent",
    string? checkName = null
)
        {
            bool isSkipped = false;
            bool success = false;

            try
            {
                CurrentTaskDescription = $"Installing {appName}...";

                if (!string.IsNullOrEmpty(checkName) && IsAppInstalled(checkName))
                {
                    Log($"   [SKIP] {appName} is already installed.");
                    isSkipped = true;
                    return;
                }

                string installerPath = Path.Combine(_assetsPath!, exeName);
                if (File.Exists(installerPath))
                {
                    if (exeName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        success = await RunProcessAsync("msiexec.exe", $"/i \"{installerPath}\" {args}", $"Installing {appName}");
                    }
                    else
                    {
                        success = await RunProcessAsync(installerPath, args, $"Installing {appName}");
                    }
                }
                else
                {
                    Log($"   [SKIP] Installer not found: {exeName}");
                    isSkipped = true;
                }
            }
            finally
            {
                if (isSkipped)
                {
                    RecordInstallResult(appName, false, true);
                }
                else
                {
                    RecordInstallResult(appName, success);
                }
                IncrementProgress();
            }
        }

        #region Package Implementations

        private async Task InstallCommonPackages(IEnumerable<string> selectedApps)
        {
            if (selectedApps.Contains("Google Chrome"))
                await SmartInstall("Google Chrome", "chrome.exe", "/silent /install", "Google Chrome");
            if (selectedApps.Contains("Mozilla Firefox"))
                await SmartInstall("Mozilla Firefox", "Firefox.exe", "-ms", "Mozilla Firefox");
            if (selectedApps.Contains("Microsoft Edge"))
                await SmartInstall("Microsoft Edge", "edge.msi", "/quiet", "Microsoft Edge");
            if (selectedApps.Contains("WinRAR"))
                await SmartInstall("WinRAR", "winrar.exe", "/S", "WinRAR");
            if (selectedApps.Contains("Revo Uninstaller Pro"))
                await SmartInstall("Revo Uninstaller", "Revo.exe", "/S", "Revo Uninstaller");
            if (selectedApps.Contains("IObit Driver Booster"))
                await SmartInstall("IObit Driver Booster", "drv.exe", "/S /I", "Driver Booster");
            if (selectedApps.Contains("Notepad++"))
                await SmartInstall("Notepad++", "npp.exe", "/S", "Notepad++");
            if (selectedApps.Contains("Mozilla Thunderbird"))
                await SmartInstall("Thunderbird", "Thunderbird.exe", "-ms -ma", "Mozilla Thunderbird");
            if (selectedApps.Contains("Sticky Notes"))
                await SmartInstall("Sticky Notes", "sticky.exe", "Setup_SimpleStickyNotes.exe /SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART", "Sticky Notes");

            if (selectedApps.Contains("Adobe Acrobat PRO DC"))
            {
                if (!IsAppInstalled("Adobe Acrobat"))
                    await InstallZipPackage("acrobat.zip", "Setup.exe", "/sAll", "Adobe Acrobat PRO");
                else
                    Log("   [SKIP] Adobe Acrobat is already installed.");
            }

            if (selectedApps.Contains("WPS Office 2020"))
            {
                await InstallZipPackage("WPS.zip", "Setup.exe", "/S /D=\"C:\\Program Files\\WPS Office\"", "WPS Office");

                Log("   [PATCH] Stopping WPS processes to unlock files...");
                await Task.Run(() =>
                {
                    string[] wpsProcs = ["wps", "wpp", "et", "wpscenter", "wpscloudsvr", "wpspdf", "wccef", "wpsupdate"];
                    foreach (var procName in wpsProcs)
                    {
                        try { foreach (var p in Process.GetProcessesByName(procName)) p.Kill(); } catch { }
                    }
                });
                await Task.Delay(2000);

                string wpsExtractDir = Path.Combine(_assetsPath!, "WPS");
                string authDllSource = Path.Combine(wpsExtractDir, "auth.dll");

                if (!File.Exists(authDllSource))
                {
                    var files = Directory.GetFiles(wpsExtractDir, "auth.dll", SearchOption.AllDirectories);
                    if (files.Length > 0) authDllSource = files[0];
                }

                EnsureWpsShortcutsForAllUsers();
                if (File.Exists(authDllSource))
                {
                    string wpsTargetDir = @"C:\Program Files\WPS Office";
                    if (Directory.Exists(wpsTargetDir))
                    {
                        var office6Dirs = Directory.GetDirectories(wpsTargetDir, "office6", SearchOption.AllDirectories);
                        if (office6Dirs.Length > 0)
                        {
                            foreach (var dir in office6Dirs)
                            {
                                string authDllDest = Path.Combine(dir, "auth.dll");
                                try
                                {
                                    File.Copy(authDllSource, authDllDest, true);
                                    Log($"   [SUCCESS] Patched: {authDllDest}");
                                }
                                catch (Exception ex) { Log($"   [ERROR] Failed to patch {dir}: {ex.Message}"); }
                            }
                        }
                        else
                        {
                            Log($"   [WARN] 'office6' folder not found in {wpsTargetDir}");
                        }
                    }
                    else
                    {
                        Log($"   [WARN] WPS Install directory not found at: {wpsTargetDir}");
                        Log("           (Installer might have failed or ignored the /D switch)");
                    }
                }
                else
                {
                    Log("   [ERROR] auth.dll source not found in Assets/WPS.");
                }
            }

            if (selectedApps.Contains("Radmin Server (+ Config)"))
            {
                await SmartInstall("Radmin Server", "radmins.msi", "/qn /quiet", "Radmin Server 3.5");
                await ApplyRadminServer();
            }

            if (selectedApps.Contains("All VC++ Redistributables"))
            {
                bool hasModernVc = IsAppInstalled("Visual C++ v14") ||
                                   IsAppInstalled("Visual C++ 2015") ||
                                   IsAppInstalled("Visual C++ 2015-2022") ||
                                   IsAppInstalled("Visual C++ 2015-2019");
                bool has2013Vc = IsAppInstalled("Visual C++ 2013");

                if (!hasModernVc || !has2013Vc)
                {
                    Log("   [INIT] Preparing VC++ Runtimes...");
                    await InstallZipPackage("vcredistAIO.zip", "install_all.bat", "", "VC++ Runtimes");
                }
                else
                {
                    Log("   [SKIP] VC++ Runtimes (Recent versions) appear installed.");
                }
            }

            await ApplyWallpaper();

            Log("   [CONFIG] Managing Taskbar Pins...");
            await ClearTaskbar();


            await PinToTaskbar("File Explorer", "explorer.exe");
            if (selectedApps.Contains("Google Chrome")) await PinToTaskbar("Google Chrome", "chrome.exe");
            if (selectedApps.Contains("Mozilla Firefox")) await PinToTaskbar("Mozilla Firefox", "firefox.exe");
            if (selectedApps.Contains("Mozilla Thunderbird")) await PinToTaskbar("Mozilla Thunderbird", "thunderbird.exe");


            Log("   [CONFIG] Setting Power Options (Sleep: Never)...");
            await RunProcessAsync("powercfg", "/change standby-timeout-ac 0", "Disable Sleep (AC)");
            await RunProcessAsync("powercfg", "/change standby-timeout-dc 0", "Disable Sleep (Battery)");
            await RunProcessAsync("powercfg", "/change monitor-timeout-ac 0", "Disable Monitor Sleep (AC)");
            await RunProcessAsync("powercfg", "/change monitor-timeout-dc 0", "Disable Monitor Sleep (Battery)");
        }

        private async Task ApplyWallpaper()
        {
            string wallpaperName = "PG-wallpaper.jpeg";
            string wallpaperPath = Path.Combine(_assetsPath!, wallpaperName);

            if (File.Exists(wallpaperPath))
            {
                Log($"   [CONFIG] Applying Wallpaper: {wallpaperName}...");
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("WallpaperStyle", "6");
                            key.SetValue("TileWallpaper", "0");
                        }
                    }
                    await Task.Run(() =>
                    {
                        SystemParametersInfo(20, 0, wallpaperPath, 3);
                    });

                    Log("   [SUCCESS] Wallpaper applied.");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to set wallpaper: {ex.Message}");
                }
            }
            else
            {
                Log($"   [WARN] Wallpaper not found: {wallpaperName}");
            }
        }

        private async Task InstallZipPackage(string zipName, string installerName, string args, string description)
        {
            string zipPath = Path.Combine(_assetsPath!, zipName);
            string extractRoot = @"C:\Assets";
            string extractPath = Path.Combine(extractRoot, Path.GetFileNameWithoutExtension(zipName));

            if (File.Exists(zipPath))
            {
                if (!Directory.Exists(extractPath))
                {
                    Log($"   [EXTRACT] Unzipping {zipName} to C:\\Assets...");
                    try
                    {
                        Directory.CreateDirectory(extractPath);
                        await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractPath));
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Extract failed: {ex.Message}");
                        return;
                    }
                }

                string setupPath = "";
                var files = Directory.GetFiles(
                    extractPath,
                    installerName,
                    SearchOption.AllDirectories
                );

                if (files.Length > 0)
                    setupPath = files[0];

                if (File.Exists(setupPath))
                {
                    if (installerName.EndsWith(".bat") || installerName.EndsWith(".cmd"))
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c \"{setupPath}\"",
                            WorkingDirectory = Path.GetDirectoryName(setupPath),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        };
                        await RunCustomProcess(startInfo, $"Installing {description}");
                    }
                    else
                    {
                        await RunProcessAsync(setupPath, args, $"Installing {description}");
                    }
                }
                else
                {
                    Log($"   [ERROR] {installerName} not found inside extracted folder.");
                }
            }
            else
            {
                Log($"   [SKIP] Zip not found: {zipName}");
            }
        }

        #endregion

        #region Helpers
        private async Task<bool> RunProcessAsync(
            string fileName,
            string arguments,
            string description,
            bool suppressError = false
        )
        {
            string? workingDir = Path.GetDirectoryName(fileName);
            if (string.IsNullOrWhiteSpace(workingDir))
            {
                workingDir = Environment.CurrentDirectory;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                WorkingDirectory = workingDir,
            };
            return await RunCustomProcess(startInfo, description, suppressError);
        }

        private async Task<bool> RunCustomProcess(
    ProcessStartInfo startInfo,
    string description,
    bool suppressError = false
)
        {
            CurrentTaskDescription = description;

            if (description.Contains("Installing", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("Deploying", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("Configuring", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("Patching", StringComparison.OrdinalIgnoreCase))
            {
                CurrentStep++;
                ProgressPercentage = TotalSteps > 0 ? Math.Min(100, (int)((double)CurrentStep / TotalSteps * 100)) : 0;
            }

            Log($"[{DateTime.Now:HH:mm:ss}] {description}...");
            var tcs = new TaskCompletionSource<bool>();
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string l = CleanLogLine(e.Data);
                    if (l != null)
                        Log($"    > {l}");
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string l = CleanLogLine(e.Data);
                    if (l != null)
                        Log($"    > {l}");
                }
            };

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
                bool success = await tcs.Task;
                return success;
            }
            catch (Exception ex)
            {
                if (!suppressError)
                    Log($"   [FAILED] Process Error: {ex.Message}");
                return false;
            }
        }
        private bool IsAppInstalled(string partialName)
        {
            string[] registryPaths =
            [
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            ];

            foreach (var path in registryPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(path);
                    if (key != null)
                    {
                        foreach (var subkeyName in key.GetSubKeyNames())
                        {
                            using var subkey = key.OpenSubKey(subkeyName);
                            var displayName = subkey?.GetValue("DisplayName") as string;
                            if (!string.IsNullOrEmpty(displayName) && displayName.Contains(partialName, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                    }
                }
                catch { }
            }
            return false;
        }

        private string CleanLogLine(string line)
        {
           
            if (string.IsNullOrWhiteSpace(line)) return null!;
            line = line.Trim();
            if (line.StartsWith("[=") || line.StartsWith("=======")) return null!;
            if (line.Contains("Extracting", StringComparison.OrdinalIgnoreCase)) return null!;
            if (line.Contains("VERBOSE1:chrome", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("installer.cc", StringComparison.OrdinalIgnoreCase)) return null!;

            if (Regex.IsMatch(line, @"\d+%$")) return null!;

            return line;
        }

        private void Log(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogOutput ??= string.Empty;
                LogOutput += $"{message}{Environment.NewLine}";
            });
        }

        private void RecordInstallResult(string appName, bool success, bool skipped = false)
        {
            if (skipped)
            {
                InstallSkipCount++;
                Log($"   [SKIP] {appName}");
            }
            else if (success)
            {
                InstallSuccessCount++;
                Log($"   [SUCCESS] {appName}");
            }
            else
            {
                InstallFailCount++;
                Log($"   [FAILED] {appName}");
            }
        }
        private void ShowInstallationSummary()
        {
            string msg = $"Installation Complete!\n\n" +
                         $"✅ Succeeded: {InstallSuccessCount}\n" +
                         $"❌ Failed: {InstallFailCount}\n" +
                         $"⏭️ Skipped: {InstallSkipCount}\n\n" +
                         $"Would you like to export the installation log?";

            if (MessageBox.Show(msg, "PG Installer Summary", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                ExportLog();
            }
        }

        private void ExportLog()
        {
            try
            {
                string logDir = @"C:\Assets\Logs";
                Directory.CreateDirectory(logDir);
                string fileName = $"{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                string filePath = Path.Combine(logDir, fileName);
                File.WriteAllText(filePath, LogOutput ?? "No log data available.");
                MessageBox.Show($"Log exported successfully to:\n{filePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RunParallelAsync(params Func<Task>[] tasks)
        {
            Log("   [PARALLEL] Executing independent tasks concurrently...");
            var executionTasks = tasks.Select(t => t()).ToArray();
            await Task.WhenAll(executionTasks);
            Log("   [PARALLEL] Concurrent tasks completed.");
        }
        #endregion
    }


}