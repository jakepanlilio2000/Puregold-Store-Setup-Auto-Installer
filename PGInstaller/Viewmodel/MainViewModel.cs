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

        private string? _sharedDatabaseIp;
        private string? _consoIpInput;
        private CancellationTokenSource? _checkInstalledCts;
        private int? _posCount;

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

        [ObservableProperty]
        private bool _joinDomainAfterInstall;

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
            PreviewList.CollectionChanged += PreviewList_CollectionChanged;
            SelectedDepartment = "IT";

            Log("Welcome to PG Installer. Select a department to begin.");
            _ = CheckDefender();
            _ = CheckSystemRestoreStatus();
            _ = CheckDomainStatusAsync();
            _ = LoadSystemInfoAsync();
            _ = CheckInstalledSoftwareAsync();

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
                    using var cpuSearcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                    foreach (var obj in cpuSearcher.Get())
                    {
                        CpuInfo = obj["Name"]?.ToString()?.Replace("  ", " ").Trim() ?? "Unknown CPU";
                        break;
                    }

                    using var ramSearcher = new System.Management.ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                    ulong totalCapacity = 0;
                    foreach (var obj in ramSearcher.Get())
                    {
                        totalCapacity += Convert.ToUInt64(obj["Capacity"]);
                    }
                    RamInfo = $"{Math.Round(totalCapacity / (1024.0 * 1024.0 * 1024.0), 1)} GB RAM";
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                    if (key != null)
                    {
                        string productName = key.GetValue("ProductName")?.ToString() ?? "Windows";
                        string edition = key.GetValue("EditionID")?.ToString() ?? "";
                        string displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? "";
                        string buildNumber = key.GetValue("CurrentBuild")?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(displayVersion) && !string.IsNullOrEmpty(buildNumber))
                        {
                            OsVersion = $"{productName} {displayVersion} (Build {buildNumber})";
                        }
                        else if (!string.IsNullOrEmpty(buildNumber))
                        {
                            OsVersion = $"{productName} (Build {buildNumber})";
                        }
                        else
                        {
                            OsVersion = productName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"   [WARN] Failed to load system info: {ex.Message}");
                    OsVersion = Environment.OSVersion.ToString();
                }
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
            if (e.NewItems != null)
            {
                foreach (InstallAppItem item in e.NewItems)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (InstallAppItem item in e.OldItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }

            UpdatePendingTasksCount();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InstallAppItem.IsChecked))
            {
                if (sender is InstallAppItem checkedItem && checkedItem.IsChecked)
                {
                    EnforceMutualExclusivity(checkedItem);
                }
                UpdatePendingTasksCount();
            }
            else if (e.PropertyName == nameof(InstallAppItem.ForceInstall))
            {
                if (sender is InstallAppItem forceItem && forceItem.ForceInstall)
                {
                    EnforceMutualExclusivity(forceItem);
                }
                UpdatePendingTasksCount();
            }
            else if (e.PropertyName == nameof(InstallAppItem.IsInstalled))
            {
                UpdatePendingTasksCount();
            }
        }

        private void EnforceMutualExclusivity(InstallAppItem activeItem)
        {
            string[] wampVersions = { "Wamp 1.7.2", "Wamp 2", "Wamp 2.5", "Wampserver 3.4.0" };
            string[] bartenderVersions = { "Bartender 10.1", "Bartender 2016", "Bartender 2022" };
            string[] bartenderDrivers = { "Argox Driver", "Zebra Driver" };
            string[] corelVersions = { "Coreldraw Graphics X5", "Coreldraw Graphics X7" };

            if (wampVersions.Contains(activeItem.Name))
            {
                foreach (var item in PreviewList)
                {
                    if (wampVersions.Contains(item.Name) && item != activeItem)
                    {
                        item.IsChecked = false;
                        item.ForceInstall = false;
                    }
                }
            }
            else if (bartenderVersions.Contains(activeItem.Name))
            {
                foreach (var item in PreviewList)
                {
                    if (bartenderVersions.Contains(item.Name) && item != activeItem)
                    {
                        item.IsChecked = false;
                        item.ForceInstall = false;
                    }
                }
            }
            else if (bartenderDrivers.Contains(activeItem.Name))
            {
                foreach (var item in PreviewList)
                {
                    if (bartenderDrivers.Contains(item.Name) && item != activeItem)
                    {
                        item.IsChecked = false;
                        item.ForceInstall = false;
                    }
                }
            }
            else if (corelVersions.Contains(activeItem.Name))
            {
                foreach (var item in PreviewList)
                {
                    if (corelVersions.Contains(item.Name) && item != activeItem)
                    {
                        item.IsChecked = false;
                        item.ForceInstall = false;
                    }
                }
            }
        }

        private void UpdatePendingTasksCount()
        {
            PendingTasksCount = PreviewList.Count(x => (!x.IsInstalled && x.IsChecked) || (x.IsInstalled && x.ForceInstall));
            OnPropertyChanged(nameof(PendingTasksCount));
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

            var selectedApps = PreviewList
                .Where(x => (!x.IsInstalled && x.IsChecked) || (x.IsInstalled && x.ForceInstall))
                .Select(x => x.Name)
                .ToList();

            if (selectedApps.Count == 0)
            {
                Log("   [WARN] No applications selected for installation.");
                return;
            }

            TotalSteps = selectedApps.Count + 4;
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

                _posCount = null;
                _consoIpInput = null;
                bool needsPosConfig = selectedApps.Any(a => a.Contains("PuTTY", StringComparison.OrdinalIgnoreCase) || 
                                                           a.Contains("WinSCP", StringComparison.OrdinalIgnoreCase));
                bool needsBookmarkConfig = selectedApps.Any(a => a.Contains("Chrome Bookmarks", StringComparison.OrdinalIgnoreCase));

                if (needsPosConfig || needsBookmarkConfig)
                {
                    await GetOrPromptConsoIpAsync();
                }

                if (needsPosConfig)
                {
                    await GetOrPromptPosCountAsync();
                }

                ApplySystemOptimizations();

                Log("   [CONFIG] Disabling Windows Firewall...");
                await RunProcessAsync("netsh", "advfirewall set allprofiles state off", "Disabling Windows Firewall", true);
                IncrementProgress();

                bool assetsReady = await PrepareAssets();
                if (!assetsReady)
                {
                    Log("CRITICAL: Failed to prepare assets. Stopping.");
                    return;
                }
                IncrementProgress();

                bool assetsVerified = await VerifyRequiredAssetsAsync(selectedApps);
                if (!assetsVerified)
                {
                    Log("CRITICAL: Asset verification failed or was cancelled by user. Stopping.");
                    return;
                }
                IncrementProgress();

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
                IncrementProgress();
                ProgressPercentage = 100;
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

        private async Task<bool> VerifyRequiredAssetsAsync(List<string> selectedApps)
        {
            Log("   [VERIFY] Validating installer files for selected applications...");
            var appToFileMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Google Chrome", new[] { "chrome.exe" } },
                { "Mozilla Firefox", new[] { "Firefox.exe" } },
                { "Microsoft Edge", new[] { "edge.msi" } },
                { "WinRAR", new[] { "winrar.exe" } },
                { "Revo Uninstaller Pro", new[] { "revo.exe" } },
                { "IObit Driver Booster", new[] { "drv.exe" } },
                { "Notepad++", new[] { "npp.exe" } },
                { "Mozilla Thunderbird", new[] { "Thunderbird.exe" } },
                { "Sticky Notes", new[] { "sticky.exe" } },
                { "Adobe Acrobat PRO DC", new[] { "acrobat.zip", "acrobat.exe" } },
                { "WPS Office 2020", new[] { "WPS.zip", "wps.exe" } },
                { "Radmin Server", new[] { "radmins.msi" } },
                { "Radmin Server (+ Config)", new[] { "radmins.msi" } },
                { "All VC++ Redistributables", new[] { "vcredistAIO.zip", "vcredist.exe" } },
                { "Zoom", new[] { "zoom.exe", "ZoomInstaller.exe" } },
                { "Advanced IP Scanner", new[] { "ipscan.exe" } },
                { "PITK", new[] { "pitk.zip", "pitk.exe" } },
                { "A&VGW", new[] { "avgw.exe" } },
                { "PuTTY", new[] { "putty.zip", "putty.exe" } },
                { "WinSCP", new[] { "winscp.zip", "winscp.exe" } },
                { "Radmin Viewer", new[] { "radminv.msi", "radminv.exe" } },
                { "PIMS", new[] { "pims.zip" } },
                { "MMS (PCOMM)", new[] { "mms.zip", "pcomm.exe" } },
                { ".NET Framework 3.5", new[] { "netfx.zip", "netfx3.cab" } },
                { "FSDM", new[] { "fsdm.zip" } },
                { "Wamp 1.7.2", new[] { "wamp1.7.exe" } },
                { "Wamp 2", new[] { "wamp2.exe" } },
                { "Wamp 2.5", new[] { "wamp2.5.exe" } },
                { "Wampserver 3.4.0", new[] { "wamp3.4.exe" } },
                { "Bartender 10.1", new[] { "bt10.1.exe" } },
                { "Bartender 2016", new[] { "bt2016.exe", "bp2016.exe" } },
                { "Bartender 2022", new[] { "bt2022.exe" } },
                { "Argox Driver", new[] { "argox.exe" } },
                { "Zebra Driver", new[] { "zebra.exe" } },
                { "Inventory Tools", new[] { "inventory.zip", "tools.zip" } },
                { "Variance", new[] { "variance.zip" } },
                { "Coreldraw Graphics X5", new[] { "cx5.exe", "corel_x5.exe" } },
                { "Coreldraw Graphics X7", new[] { "cx7.exe", "corel_x7.exe" } },
                { "Photoshop CS6", new[] { "Photoshop_13_LS16.7z", "photoshop.zip" } },
                { "Illustrator CS6", new[] { "Illustrator_16_LS16.7z", "illustrator.zip" } },
                { "Oracle Java Runtime", new[] { "jre.exe", "java.exe" } },
                { "Java Oracle", new[] { "jre.exe", "java.exe" } },
                { "VLC Media Player", new[] { "vlc.exe" } }
            };

            var missing = new List<string>();

            foreach (var app in selectedApps)
            {
                if (!appToFileMap.TryGetValue(app, out var candidateFiles)) continue;

                if (IsAppInstalled(app) && !IsForceInstall(app, null)) continue;

                bool found = false;
                foreach (var file in candidateFiles)
                {
                    string? path = ResolveAssetPath(file);
                    if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
                    {
                        found = true;
                        break;
                    }

                    // Attempt on-demand extraction from assets.zip
                    if (await ExtractSpecificFile(null, $"*{file}*"))
                    {
                        path = ResolveAssetPath(file);
                        if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    string expected = string.Join(" or ", candidateFiles);
                    missing.Add($"{app} (File: {expected})");
                    Log($"   [WARN] Missing installer asset for '{app}' (Expected: {expected})");
                }
            }

            if (missing.Count > 0)
            {
                Log($"   [VERIFY FAILED] {missing.Count} application installer(s) missing from Assets.");

                var userChoice = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    string msg = $"The Assets folder is present, but {missing.Count} installer file(s) are missing:\n\n" +
                                 string.Join("\n", missing.Take(8).Select(m => $" • {m}")) +
                                 (missing.Count > 8 ? $"\n ...and {missing.Count - 8} more" : "") +
                                 "\n\nWould you like to proceed with installing only the available packages?";
                    return MessageBox.Show(msg, "Missing Installer Files", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                });

                if (userChoice != MessageBoxResult.Yes)
                {
                    return false;
                }
            }
            else
            {
                Log("   [VERIFY SUCCESS] All required installer files verified.");
            }

            return true;
        }

        [RelayCommand]
        private void SelectAllManifest()
        {
            foreach (var item in FilteredPreviewList.OfType<InstallAppItem>())
            {
                if (!item.IsInstalled)
                {
                    item.IsChecked = true;
                }
            }
            UpdatePendingTasksCount();
        }

        [RelayCommand]
        private void DeselectAllManifest()
        {
            foreach (var item in FilteredPreviewList.OfType<InstallAppItem>())
            {
                item.IsChecked = false;
            }
            UpdatePendingTasksCount();
        }

        [RelayCommand]
        private void ClearLog()
        {
            LogOutput = string.Empty;
        }

        [RelayCommand]
        private void CopyLog()
        {
            if (!string.IsNullOrEmpty(LogOutput))
            {
                try
                {
                    Clipboard.SetText(LogOutput);
                    Log("   [INFO] Terminal output copied to clipboard.");
                }
                catch { }
            }
        }

        private async Task<string> GetOrPromptConsoIpAsync()
        {
            if (!string.IsNullOrWhiteSpace(_consoIpInput)) return _consoIpInput;

            string defaultIp = !string.IsNullOrWhiteSpace(TargetIp) ? TargetIp.Trim() : "192.168.1.101";

            string input = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter Store / Conso IP (for PuTTY, WinSCP, & Bookmarks):", defaultIp));

            if (!string.IsNullOrWhiteSpace(input))
            {
                _consoIpInput = input.Trim();
                TargetIp = _consoIpInput;
                Log($"   [CONFIG] Store/Conso IP set to: {_consoIpInput}");
            }
            else
            {
                Log($"   [INFO] No IP entered. Defaulting to {defaultIp}.");
                _consoIpInput = defaultIp;
            }

            return _consoIpInput;
        }

        private async Task<int> GetOrPromptPosCountAsync()
        {
            if (_posCount.HasValue) return _posCount.Value;

            string input = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("How many POS terminals does this store have?", "10"));

            if (int.TryParse(input?.Trim(), out int count) && count > 0)
            {
                _posCount = count;
            }
            else
            {
                Log("   [INFO] Invalid or cancelled POS count input. Defaulting to 10 POS terminals.");
                _posCount = 10;
            }

            return _posCount.Value;
        }

        private async Task SmartInstall(
            string appName,
            string exeName,
            string args = "/silent",
            string? checkName = null,
            bool? force = null
        )
        {
            bool isSkipped = false;
            bool success = false;

            try
            {
                CurrentTaskDescription = $"Installing {appName}...";

                bool isForceInstall = force ?? IsForceInstall(appName, checkName);

                if (!isForceInstall && !string.IsNullOrEmpty(checkName) && IsAppInstalled(checkName))
                {
                    Log($"   [SKIP] {appName} is already installed.");
                    isSkipped = true;
                    return;
                }

                string? installerPath = ResolveAssetPath(exeName);
                if (!string.IsNullOrEmpty(installerPath) && File.Exists(installerPath))
                {
                    if (installerPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
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
                await SmartInstall("WinRAR", "winrar.exe", "/S /EI", "WinRAR");
            if (selectedApps.Contains("Revo Uninstaller Pro"))
            {
                await SmartInstall("Revo Uninstaller", "revo.exe", "/S /EI", "Revo Uninstaller");
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\VS Revo Group\Revo Uninstaller Pro\General");
                    key?.SetValue("CurrentLanguage", "English");
                    key?.SetValue("Language", "English");
                }
                catch { }
            }
            if (selectedApps.Contains("IObit Driver Booster"))
                await SmartInstall("IObit Driver Booster", "drv.exe", "/S /EI", "Driver Booster");
            if (selectedApps.Contains("Notepad++"))
                await SmartInstall("Notepad++", "npp.exe", "/S", "Notepad++");
            if (selectedApps.Contains("Mozilla Thunderbird"))
                await SmartInstall("Thunderbird", "Thunderbird.exe", "-ms -ma", "Mozilla Thunderbird");
            if (selectedApps.Contains("Sticky Notes"))
                await SmartInstall("Sticky Notes", "sticky.exe", "Setup_SimpleStickyNotes.exe /SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART", "Sticky Notes");

            if (selectedApps.Contains("Adobe Acrobat PRO DC"))
            {
                bool forceAcrobat = IsForceInstall("Adobe Acrobat PRO DC", "Adobe Acrobat");
                if (forceAcrobat || !IsAppInstalled("Adobe Acrobat"))
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

            if (selectedApps.Contains("Radmin Server (+ Config)") || selectedApps.Contains("Radmin Server"))
            {
                await SmartInstall("Radmin Server", "radmins.msi", "/qn /quiet", "Radmin Server 3.5");
                await ApplyRadminServer();
            }

            if (selectedApps.Contains("All VC++ Redistributables"))
            {
                bool forceVc = IsForceInstall("All VC++ Redistributables", "Visual C++");
                bool hasModernVc = IsAppInstalled("Visual C++ v14") ||
                                   IsAppInstalled("Visual C++ 2015") ||
                                   IsAppInstalled("Visual C++ 2015-2022") ||
                                   IsAppInstalled("Visual C++ 2015-2019");
                bool has2013Vc = IsAppInstalled("Visual C++ 2013");

                if (forceVc || !hasModernVc || !has2013Vc)
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
            string? wallpaperPath = ResolveAssetPath(wallpaperName);

            if (!string.IsNullOrEmpty(wallpaperPath) && File.Exists(wallpaperPath))
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
            string? zipPath = ResolveAssetPath(zipName);
            string extractRoot = @"C:\Assets";
            string extractPath = Path.Combine(extractRoot, Path.GetFileNameWithoutExtension(zipName));

            if (!string.IsNullOrEmpty(zipPath) && File.Exists(zipPath))
            {
                if (!Directory.Exists(extractPath))
                {
                    Log($"   [EXTRACT] Unzipping {zipName} to C:\\Assets...");
                    try
                    {
                        Directory.CreateDirectory(extractPath);
                        await ExtractWithProgress(zipPath, extractPath, CreateStepProgress($"Unzipping {zipName}"));
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

        /// <summary>
        /// Extracts a zip archive to a destination directory reporting real-time progress (0-100%).
        /// </summary>
        /// <param name="zipPath">Path to the zip file to extract.</param>
        /// <param name="extractPath">Target directory path.</param>
        /// <param name="progress">Progress callback reporting percentage.</param>
        /// <param name="overwrite">Whether to overwrite existing files.</param>
        public async Task ExtractWithProgress(string zipPath, string extractPath, IProgress<int>? progress = null, bool overwrite = true)
        {
            if (!File.Exists(zipPath))
            {
                Log($"   [ERROR] Archive not found for extraction: {zipPath}");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(extractPath);
                    using var archive = ZipFile.OpenRead(zipPath);
                    int totalEntries = archive.Entries.Count;
                    if (totalEntries == 0)
                    {
                        progress?.Report(100);
                        return;
                    }

                    int processed = 0;
                    int lastReportedPercent = -1;

                    foreach (var entry in archive.Entries)
                    {
                        string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                        if (!destinationPath.StartsWith(Path.GetFullPath(extractPath), StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            string? parentDir = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);

                            entry.ExtractToFile(destinationPath, overwrite);
                        }

                        processed++;
                        int currentPercent = (int)((double)processed / totalEntries * 100);
                        if (currentPercent != lastReportedPercent)
                        {
                            lastReportedPercent = currentPercent;
                            progress?.Report(currentPercent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Extraction exception: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Creates an IProgress instance that scales progress within the current step of TotalSteps.
        /// </summary>
        /// <param name="operationName">Name of the operation shown in CurrentTaskDescription.</param>
        public IProgress<int> CreateStepProgress(string operationName)
        {
            return new Progress<int>(pct =>
            {
                CurrentTaskDescription = $"{operationName}... {pct}%";
                if (TotalSteps > 0)
                {
                    double basePct = (double)CurrentStep / TotalSteps * 100.0;
                    double stepWeight = 100.0 / TotalSteps;
                    ProgressPercentage = Math.Min(100, (int)(basePct + (stepWeight * pct / 100.0)));
                }
                else
                {
                    ProgressPercentage = pct;
                }
            });
        }

        #endregion

        #region Helpers

        private bool IsForceInstall(params string?[] appNames)
        {
            var validNames = appNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList();
            if (validNames.Count == 0) return false;

            return PreviewList.Any(item => item.ForceInstall && validNames.Any(name =>
                item.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                item.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(item.Name, StringComparison.OrdinalIgnoreCase)));
        }

        public async Task CheckInstalledSoftwareAsync()
        {
            _checkInstalledCts?.Cancel();
            var cts = new CancellationTokenSource();
            _checkInstalledCts = cts;

            try
            {
                await Task.Run(() =>
                {
                    var items = PreviewList.ToList();
                    foreach (var item in items)
                    {
                        if (cts.Token.IsCancellationRequested) return;

                        bool installed = CheckIfAppIsInstalled(item.Name);

                        if (cts.Token.IsCancellationRequested) return;

                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            if (cts.Token.IsCancellationRequested) return;

                            item.IsInstalled = installed;
                            if (installed)
                            {
                                item.IsChecked = false;
                            }
                        });
                    }

                    if (!cts.Token.IsCancellationRequested)
                    {
                        Application.Current?.Dispatcher?.Invoke(UpdatePendingTasksCount);
                    }
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Operation canceled due to department switch
            }
        }

        private bool CheckIfAppIsInstalled(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName)) return false;

            if (appName.Equals("MMS (PCOMM)", StringComparison.OrdinalIgnoreCase))
            {
                return IsAppInstalled("Personal Communications") || IsAppInstalled("PCOMM") || IsAppInstalled("MMS");
            }

            if (appName.Equals("Sticky Notes", StringComparison.OrdinalIgnoreCase))
            {
                return IsAppInstalled("Sticky Notes") || IsAppInstalled("Simple Sticky Notes");
            }

            if (appName.Equals("All VC++ Redistributables", StringComparison.OrdinalIgnoreCase))
            {
                return IsAppInstalled("Visual C++ v14") ||
                       IsAppInstalled("Visual C++ 2015") ||
                       IsAppInstalled("Visual C++ 2015-2022") ||
                       IsAppInstalled("Visual C++ 2015-2019") ||
                       IsAppInstalled("Visual C++");
            }

            if (appName.Equals("PITK", StringComparison.OrdinalIgnoreCase))
            {
                return IsAppInstalled("PITK") ||
                       IsAppInstalled("Puregold IT Toolkit") ||
                       File.Exists(@"C:\Program Files (x86)\Puregold IT Toolkit\PuregoldITToolkit.exe") ||
                       File.Exists(@"C:\Program Files\Puregold IT Toolkit\PuregoldITToolkit.exe");
            }

            if (appName.Equals("A&VGW", StringComparison.OrdinalIgnoreCase))
            {
                return IsAppInstalled("A&VGW") ||
                       IsAppInstalled("Annual & Variance Gateway") ||
                       IsAppInstalled("Annual and Variance Gateway") ||
                       File.Exists(@"C:\Program Files (x86)\Annual & Variance Gateway\Annual And Variance Gateway.exe") ||
                       File.Exists(@"C:\Program Files\Annual & Variance Gateway\Annual And Variance Gateway.exe");
            }

            string searchTerm = GetRegistrySearchTerm(appName);
            if (IsAppInstalled(searchTerm)) return true;
            if (!searchTerm.Equals(appName, StringComparison.OrdinalIgnoreCase) && IsAppInstalled(appName)) return true;

            return false;
        }

        private string GetRegistrySearchTerm(string appName)
        {
            return appName switch
            {
                "Google Chrome" => "Chrome",
                "Mozilla Firefox" => "Firefox",
                "Microsoft Edge" => "Edge",
                "WinRAR" => "WinRAR",
                "Notepad++" => "Notepad++",
                "Mozilla Thunderbird" => "Thunderbird",
                "Oracle Java Runtime" => "Java",
                "All VC++ Redistributables" => "Visual C++",
                "WPS Office 2020" => "WPS Office",
                "Revo Uninstaller Pro" => "Revo Uninstaller",
                "Adobe Acrobat PRO DC" => "Adobe Acrobat",
                "Sticky Notes" => "Sticky Notes",
                "IObit Driver Booster" => "Driver Booster",
                "Radmin Server" => "Radmin Server",
                "Zoom" => "Zoom",
                "Advanced IP Scanner" => "Advanced IP Scanner",
                "PITK" => "PITK",
                "A&VGW" => "A&VGW",
                "PuTTY" => "PuTTY",
                "WinSCP" => "WinSCP",
                "Radmin Viewer" => "Radmin Viewer",
                "PIMS" => "PIMS",
                "MMS (PCOMM)" => "Personal Communications",
                "Chrome Bookmarks (CBM)" => "Chrome Bookmarks",
                ".NET Framework 3.5" => ".NET Framework 3.5",
                "FSDM" => "FSDM",
                "Wamp 1.7.2" or "Wamp 2" or "Wamp 2.5" or "Wampserver 3.4.0" => "Wamp",
                "Bartender 10.1" or "Bartender 2016" or "Bartender 2022" => "BarTender",
                "Argox Driver" => "Argox",
                "Zebra Driver" => "Zebra",
                "Inventory Tools" => "Inventory Tools",
                "Variance" => "Variance",
                "Coreldraw Graphics X5" or "Coreldraw Graphics X7" => "Corel",
                "Photoshop CS6" => "Photoshop",
                "Illustrator CS6" => "Illustrator",
                "Java Oracle" => "Java",
                "VLC Media Player" => "VLC",
                _ => appName
            };
        }

        private bool IsAppInstalled(string partialName)
        {
            if (string.IsNullOrWhiteSpace(partialName)) return false;

            string[] registryPaths =
            [
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            ];

            var rootKeys = new[] { Registry.LocalMachine, Registry.CurrentUser };

            foreach (var rootKey in rootKeys)
            {
                foreach (var path in registryPaths)
                {
                    try
                    {
                        using var key = rootKey.OpenSubKey(path);
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
            }
            return false;
        }

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

            Log($"[{DateTime.Now:HH:mm:ss}] {description}...");
            var tcs = new TaskCompletionSource<bool>();
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    ParseOutputProgress(e.Data);
                    string l = CleanLogLine(e.Data);
                    if (l != null)
                        Log($"    > {l}");
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    ParseOutputProgress(e.Data);
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

        private void ParseOutputProgress(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return;
            var match = Regex.Match(data, @"\b(\d{1,3})\s*%");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int pct) && pct >= 0 && pct <= 100)
            {
                if (TotalSteps > 0)
                {
                    double basePct = (double)CurrentStep / TotalSteps * 100.0;
                    double stepWeight = 100.0 / TotalSteps;
                    ProgressPercentage = Math.Min(100, (int)(basePct + (stepWeight * pct / 100.0)));
                }
                else
                {
                    ProgressPercentage = pct;
                }
            }
        }

        private string CleanLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null!;
            line = line.Trim();
            if (line.StartsWith("[=") || line.StartsWith("=======")) return null!;
            if (line.Contains("Extracting", StringComparison.OrdinalIgnoreCase)) return null!;
            if (line.Contains("VERBOSE1:chrome", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("installer.cc", StringComparison.OrdinalIgnoreCase)) return null!;

            // Suppress PowerShell CLIXML serialization streams
            if (line.StartsWith("#< CLIXML", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("<Objs", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("</Objs>", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("<Obj", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("<TN", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("<T>", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("<MS>", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("<PR", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("<AV>", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("<S S=", StringComparison.OrdinalIgnoreCase))
            {
                return null!;
            }

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

        private string? ResolveAssetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            string basePath = _assetsPath ?? string.Empty;
            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
            {
                basePath = Directory.Exists(@"C:\Assets")
                    ? @"C:\Assets"
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            }

            // 1. Direct check in assets folder or absolute path
            if (Path.IsPathRooted(fileName) && File.Exists(fileName))
            {
                return fileName;
            }

            string directPath = Path.Combine(basePath, fileName);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            // 2. Check subdirectories of the assets folder
            string pureName = Path.GetFileName(fileName);
            if (Directory.Exists(basePath))
            {
                try
                {
                    var existingFiles = Directory.GetFiles(basePath, pureName, SearchOption.AllDirectories);
                    if (existingFiles.Length > 0)
                    {
                        return existingFiles[0];
                    }
                }
                catch { }

                // 3. Fallback: scan all .zip archives in basePath
                try
                {
                    var zipFiles = Directory.GetFiles(basePath, "*.zip", SearchOption.AllDirectories);
                    foreach (var zipPath in zipFiles)
                    {
                        try
                        {
                            using var archive = ZipFile.OpenRead(zipPath);
                            var matchEntry = archive.Entries.FirstOrDefault(e =>
                                !string.IsNullOrEmpty(e.Name) &&
                                (e.FullName.Equals(fileName.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) ||
                                 e.FullName.EndsWith("/" + fileName.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
                                ?? archive.Entries.FirstOrDefault(e =>
                                !string.IsNullOrEmpty(e.Name) &&
                                string.Equals(e.Name, pureName, StringComparison.OrdinalIgnoreCase));

                            if (matchEntry != null)
                            {
                                Log($"   [RESOLVE] Found '{pureName}' inside '{Path.GetFileName(zipPath)}'. Extracting...");

                                foreach (var entry in archive.Entries)
                                {
                                    if (string.IsNullOrEmpty(entry.Name)) continue;

                                    string destFilePath = Path.Combine(basePath, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                                    string? destDir = Path.GetDirectoryName(destFilePath);
                                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                                    {
                                        Directory.CreateDirectory(destDir);
                                    }

                                    try
                                    {
                                        entry.ExtractToFile(destFilePath, overwrite: true);
                                    }
                                    catch { }
                                }

                                string targetExtracted = Path.Combine(basePath, matchEntry.FullName.Replace('/', Path.DirectorySeparatorChar));
                                if (File.Exists(targetExtracted))
                                {
                                    return targetExtracted;
                                }

                                string directExtract = Path.Combine(basePath, pureName);
                                if (File.Exists(directExtract))
                                {
                                    return directExtract;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"   [WARN] Could not inspect archive '{Path.GetFileName(zipPath)}': {ex.Message}");
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        #endregion
    }
}
