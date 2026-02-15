using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace PGInstaller.Viewmodel
{

    public partial class MainViewModel : ObservableObject
    {

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [ObservableProperty]
        private string? _logOutput;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _selectedDepartment;

        private string _assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");

        public ObservableCollection<string> PreviewList { get; } =
            new ObservableCollection<string>();

        public ObservableCollection<string> Departments { get; } =
            new ObservableCollection<string>
            {
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
            };

        public MainViewModel()
        {
            SelectedDepartment = "IT";
            Log("Welcome to PG Installer. Select a department to begin.");
            _ = CheckDefender();
            _ = CheckSystemRestoreStatus();
        }

        [RelayCommand]
        private async Task Install()
        {
            if (IsBusy)
                return;

            if (IsRestorePointEnabled)
            {
                IsBusy = true;
                await CreateSystemRestorePoint();
                IsBusy = false;
            }

            IsBusy = true;
            LogOutput = "";
            Log("------------------------------------------------");
            Log($"Starting Installation for: {SelectedDepartment}");

            try
            {
                bool assetsReady = await PrepareAssets();

                if (!assetsReady)
                {
                    Log("CRITICAL: Failed to prepare assets. Stopping.");
                    return;
                }

                switch (SelectedDepartment)
                {
                    case "IT":
                        await InstallITPackage();
                        break;
                    case "HRD":
                        await InstallHRDPackage();
                        break;
                    case "ICD":
                        await InstallICDPackage();
                        break;
                    case "Payables":
                        await InstallPayablesPackage();
                        break;
                    case "Admin":
                        await InstallAdminPackage();
                        break;
                    case "Audit":
                        await InstallAuditPackage();
                        break;
                    case "Store Operations (Manager)":
                        await InstallStoreOperationsPackage("Manager");
                        break;
                    case "Store Operations (Customer Service)":
                        await InstallStoreOperationsPackage("Customer Service");
                        break;
                    case "Store Operations (Selling)":
                        await InstallStoreOperationsPackage("Selling");
                        break;
                    case "Store Operations (HBC)":
                        await InstallStoreOperationsPackage("HBC");
                        break;
                    case "Creative":
                        await InstallCreativePackage();
                        break;
                    case "Receiving":
                        await InstallReceivingPackage();
                        break;
                    case "Treasury":
                        await InstallTreasuryPackage();
                        break;
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
            }
        }

        private async Task SmartInstall(
            string appName,
            string exeName,
            string args = "/silent",
            string? checkName = null
        )
        {
            if (!string.IsNullOrEmpty(checkName) && IsAppInstalled(checkName))
            {
                Log($"   [SKIP] {appName} is already installed.");
                return;
            }

            string installerPath = Path.Combine(_assetsPath, exeName);

            if (File.Exists(installerPath))
            {
                if (exeName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                {
                    await RunProcessAsync(
                        "msiexec.exe",
                        $"/i \"{installerPath}\" {args}",
                        $"Installing {appName}"
                    );
                }
                else
                {
                    await RunProcessAsync(installerPath, args, $"Installing {appName}");
                }
            }
            else
            {
                Log($"   [SKIP] Installer not found: {exeName}");
            }
        }



        #region Package Implementations

        private async Task InstallCommonPackages()
        {
            await SmartInstall("Google Chrome", "chrome.exe", "/silent /install", "Google Chrome");
            await SmartInstall("Mozilla Firefox", "Firefox.exe", "-ms", "Mozilla Firefox");
            await SmartInstall("Microsoft Edge", "edge.msi", "/quiet", "Microsoft Edge");
            await SmartInstall("WinRAR", "winrar.exe", "/S", "WinRAR");
            await SmartInstall("Revo Uninstaller", "Revo.exe", "/S", "Revo Uninstaller");
            await SmartInstall("IObit Driver Booster", "drv.exe", "/S /I", "Driver Booster");
            await SmartInstall("Notepad++", "npp.exe", "/S", "Notepad++");
            await SmartInstall("Thunderbird", "Thunderbird.exe", "-ms -ma", "Mozilla Thunderbird");
            await SmartInstall("Radmin Server", "radmins.msi", "/qn /quiet", "Radmin Server 3.5");
            await SmartInstall("Sticky Notes", "sticky.exe", "Setup_SimpleStickyNotes.exe /SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART", "Sticky Notes");

            if (!IsAppInstalled("Microsoft Visual C++ 2015-2022") || !IsAppInstalled("Microsoft Visual C++ 2013"))
            {
                Log("   [INIT] Preparing VC++ Runtimes...");
                await InstallZipPackage("vcredistAIO.zip", "install_all.bat", "", "VC++ Runtimes");
            }
            else
            {
                Log("   [SKIP] VC++ Runtimes (Recent versions) appear installed.");
            }

            if (!IsAppInstalled("Adobe Acrobat"))
            {
                await InstallZipPackage("acrobat.zip", "Setup.exe", "/sAll", "Adobe Acrobat PRO");
            }
            else
            {
                Log("   [SKIP] Adobe Acrobat is already installed.");
            }

            await InstallZipPackage("WPS.zip", "Setup.exe", "/S /D=\"C:\\Program Files\\WPS Office\"", "WPS Office");

            Log("   [PATCH] Stopping WPS processes to unlock files...");
            await Task.Run(() =>
            {
                string[] wpsProcs = { "wps", "wpp", "et", "wpscenter", "wpscloudsvr", "wpspdf", "wccef", "wpsupdate" };
                foreach (var procName in wpsProcs)
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName(procName)) p.Kill();
                    }
                    catch { }
                }
            });
            await Task.Delay(2000);

            string wpsExtractDir = Path.Combine(_assetsPath, "WPS");
            string authDllSource = Path.Combine(wpsExtractDir, "auth.dll");

            if (!File.Exists(authDllSource))
            {
                var files = Directory.GetFiles(wpsExtractDir, "auth.dll", SearchOption.AllDirectories);
                if (files.Length > 0) authDllSource = files[0];
            }

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

            await ApplyRadminServer();
            await ApplyWallpaper();

            Log("   [CONFIG] Pinning Shortcuts to Taskbar...");
            await PinToTaskbar("Google Chrome", "chrome.exe");
            await PinToTaskbar("Mozilla Firefox", "firefox.exe");
            await PinToTaskbar("Mozilla Thunderbird", "thunderbird.exe");

            Log("   [CONFIG] Setting Power Options (Sleep: Never)...");
            await RunProcessAsync("powercfg", "/change standby-timeout-ac 0", "Disable Sleep (AC)");
            await RunProcessAsync("powercfg", "/change standby-timeout-dc 0", "Disable Sleep (Battery)");
            await RunProcessAsync("powercfg", "/change monitor-timeout-ac 0", "Disable Monitor Sleep (AC)");
            await RunProcessAsync("powercfg", "/change monitor-timeout-dc 0", "Disable Monitor Sleep (Battery)");
        }
        private async Task ApplyWallpaper()
        {
            string wallpaperName = "PG-wallpaper.jpeg";
            string wallpaperPath = Path.Combine(_assetsPath, wallpaperName);

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
            string zipPath = Path.Combine(_assetsPath, zipName);
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
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(fileName),
            };
            return await RunCustomProcess(startInfo, description, suppressError);
        }

        private async Task<bool> RunCustomProcess(
            ProcessStartInfo startInfo,
            string description,
            bool suppressError = false
        )
        {
            Log($"[{DateTime.Now:HH:mm:ss}] {description}...");
            var tcs = new TaskCompletionSource<bool>();
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string? l = CleanLogLine(e.Data);
                    if (l != null)
                        Log($"   > {l}");
                }
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string? l = CleanLogLine(e.Data);
                    if (l != null)
                        Log($"   > {l}");
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
                await tcs.Task;
                return true;
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
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            };

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
                            if (
                                !string.IsNullOrEmpty(displayName)
                                && displayName.Contains(
                                    partialName,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                                return true;
                        }
                    }
                }
                catch { }
            }
            return false;
        }

        private string? CleanLogLine(string line)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line))
                return null;
            if (line.StartsWith("[=") || line.StartsWith("======="))
                return null;
            if (line.Contains("Extracting"))
                return null;
            if (Regex.IsMatch(line, @"\d+%$"))
                return null;
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
