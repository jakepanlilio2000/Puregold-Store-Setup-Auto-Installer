using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Windows;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallMMS()
        {
            string checkAppName = "IBM i Access for Windows 7.1";

            try
            {
                using (var sessionMgr = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager", true))
                {
                    sessionMgr?.DeleteValue("PendingFileRenameOperations", false);
                }

                using (var wu = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update", true))
                {
                    wu?.DeleteSubKey("RebootRequired", false);
                }
            }
            catch (Exception ex)
            {
                Log($"   [WARN] Could not clear reboot flags: {ex.Message}");
            }

            string? mmsZip = ResolveAssetPath("mms.zip");
            string mmsExtractedDir = Path.Combine(_assetsPath ?? @"C:\Assets", "mms");

            if (!string.IsNullOrEmpty(mmsZip) && File.Exists(mmsZip) && !Directory.Exists(mmsExtractedDir))
            {
                Log("   [INIT] Extracting mms.zip...");
                try
                {
                    Directory.CreateDirectory(mmsExtractedDir);
                    await ExtractWithProgress(mmsZip, mmsExtractedDir, CreateStepProgress("Extracting MMS"));
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to extract mms.zip: {ex.Message}");
                }
            }
            string relativeMsi = @"mms\image64a\cwbinstall.msi";
            string? fullMstPath = ResolveAssetPath(@"mms\image64a\1033.mst") ?? Path.Combine(_assetsPath ?? @"C:\Assets", @"mms\image64a\1033.mst");
            string msiArgs = $"TRANSFORMS=\"{fullMstPath}\" /qn /norestart";

            await SmartInstall("IBM i Access 7.1", relativeMsi, msiArgs, checkAppName);

            string mmsFileName = "MMS.ws";
            string? mmsSource = ResolveAssetPath(mmsFileName);
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            string mmsDest = Path.Combine(publicDesktop, mmsFileName);

            if (!string.IsNullOrEmpty(mmsSource) && File.Exists(mmsSource) && !File.Exists(mmsDest))
            {
                try
                {
                    File.Copy(mmsSource, mmsDest);
                    Log($"   [COPY] Copied {mmsFileName} to All Users Desktop.");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to copy {mmsFileName}: {ex.Message}");
                }
            }
            string kmpFileName = "AS400.KMP";
            string? kmpSource = ResolveAssetPath(kmpFileName);
            string kmpDest = @"C:\AS400.KMP";

            if (!string.IsNullOrEmpty(kmpSource) && File.Exists(kmpSource))
            {
                try
                {
                    File.Copy(kmpSource, kmpDest, true);
                    Log($"   [COPY] Copied {kmpFileName} to C:\\.");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to copy {kmpFileName}: {ex.Message}");
                }
            }
        }
        private async Task ApplyRadminServer()
        {
            string? installBatPath = ResolveAssetPath("install.bat");
            if (!string.IsNullOrEmpty(installBatPath) && File.Exists(installBatPath))
            {
                string? dll1 = ResolveAssetPath("newtstop.dll");
                string? dll2 = ResolveAssetPath("nts64helper.dll");
                if (
                    !string.IsNullOrEmpty(dll1) && File.Exists(dll1)
                    && !string.IsNullOrEmpty(dll2) && File.Exists(dll2)
                )
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{installBatPath}\"",
                        WorkingDirectory = Path.GetDirectoryName(installBatPath),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    await RunCustomProcess(startInfo, "Applying Radmin NewTrialStop Patch");
                }
                else
                {
                    Log("   [ERROR] Dependencies for install.bat (newtstop.dll or nts64helper.dll) missing.");
                }
            }
            else
            {
                Log("   [WARNING] install.bat not found in Assets.");
            }
            IncrementProgress();
        }

        private async Task InstallInventoryTools()
        {
            string zipName = "inventorytools.zip";
            string? zipPath = ResolveAssetPath(zipName);
            string targetDir = @"C:\wamp64\www\puregold";

            if (!string.IsNullOrEmpty(zipPath) && File.Exists(zipPath))
            {
                Log($"   [DEPLOY] Deploying {zipName}...");

                try
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    await ExtractWithProgress(zipPath, targetDir, CreateStepProgress("Extracting Inventory Tools"), overwrite: true);

                    Log("   [SUCCESS] Inventory Tools deployed.");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to deploy Inventory Tools: {ex.Message}");
                }
            }
            else
            {
                Log($"   [WARN] {zipName} not found in Assets.");
            }
            IncrementProgress();
        }

        private async Task ClearTaskbar()
        {
            Log("   [CONFIG] Clearing existing Taskbar pins...");

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband", true);
                if (key != null)
                {
                    key.DeleteValue("Favorites", false);
                    key.DeleteValue("FavoritesResolve", false);
                    key.SetValue("FavoritesVersion", 3, RegistryValueKind.DWord);

                    int changes = 0;
                    if (key.GetValue("FavoritesChanges") is int currentChanges) changes = currentChanges;
                    key.SetValue("FavoritesChanges", changes + 1, RegistryValueKind.DWord);
                }
                string taskBarDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

                if (Directory.Exists(taskBarDir))
                {
                    foreach (var file in Directory.GetFiles(taskBarDir, "*.lnk"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }

                Log("   [SUCCESS] Taskbar cleared (including default UWP apps).");
            }
            catch (Exception ex)
            {
                Log($"   [WARN] Failed to clear Taskbar: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        private async Task PinToTaskbar(string appName, string exeName)
        {
            string[] searchPaths = [
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        @"C:\Program Files\Google\Chrome\Application",
        @"C:\Program Files\Mozilla Firefox",
        @"C:\Program Files\Mozilla Thunderbird"
            ];

            string? targetPath = null;

            if (appName.Equals("File Explorer", StringComparison.OrdinalIgnoreCase))
            {
                targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            }
            else
            {
                foreach (var dir in searchPaths)
                {
                    if (Directory.Exists(dir))
                    {
                        try
                        {
                            var files = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories)
                                .Concat(Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly));

                            var match = files.FirstOrDefault(f =>
                                Path.GetFileName(f).Equals(exeName, StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileNameWithoutExtension(f).Equals(appName, StringComparison.OrdinalIgnoreCase));

                            if (match != null)
                            {
                                targetPath = match;
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }

            if (targetPath != null)
            {
                Log($"   [CONFIG] Pinning {appName} to Taskbar...");
                string? scriptPath = ResolveAssetPath("Pin-Taskbar.ps1");

                if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
                {
                    string safePath = targetPath.Replace("'", "''");

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Pin \"{safePath}\" -Silent",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    bool success = await RunCustomProcess(startInfo, $"Pinning {appName}", true);

                    if (success)
                    {
                        Log($"   [SUCCESS] Pinned {appName} to Taskbar.");
                    }
                    else
                    {
                        Log($"   [WARN] Pinning script reported failure for {appName}.");
                    }
                }
                else
                {
                    Log($"   [ERROR] Pin-Taskbar.ps1 not found in Assets folder.");
                }
            }
            else
            {
                Log($"   [WARN] Could not find {appName} ({exeName}) to pin.");
            }
        }
        private async Task InstallAVGW()
        {
            Log("------------------------------------------------");
            Log("   [INIT] Installing Annual & Variance Gateway (A&VGW)...");

            string targetExe = @"C:\Program Files (x86)\Annual & Variance Gateway\Annual And Variance Gateway.exe";
            if (!File.Exists(targetExe))
            {
                string altExe = @"C:\Program Files\Annual & Variance Gateway\Annual And Variance Gateway.exe";
                if (File.Exists(altExe))
                {
                    targetExe = altExe;
                }
            }

            bool isForceInstall = IsForceInstall("A&VGW", "Annual & Variance Gateway");
            bool isInstalled = IsAppInstalled("A&VGW") || IsAppInstalled("Annual & Variance Gateway") || File.Exists(targetExe);

            if (!isForceInstall && isInstalled)
            {
                Log("   [SKIP] Annual & Variance Gateway (A&VGW) is already installed.");
                RecordInstallResult("A&VGW", false, true);
                await CreateAndVerifyDesktopShortcut("Annual & Variance Gateway", targetExe);
                IncrementProgress();
                return;
            }

            string exeName = "A&VGWSetup.exe";
            string? installerPath = ResolveAssetPath(exeName);

            if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
            {
                Log($"   [ERROR] {exeName} not found in Assets.");
                RecordInstallResult("A&VGW", false);
                IncrementProgress();
                return;
            }

            if (string.IsNullOrEmpty(_sharedDatabaseIp))
            {
                _sharedDatabaseIp = await Application.Current.Dispatcher.InvokeAsync(() =>
                    ShowInputDialog("Enter Database IP / Host for A&VGW:", "192.92.1.100")
                );
            }

            string storeNum = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter Default Store Number for A&VGW:", "722")
            );

            string fallbackStore = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter Fallback Store Name for A&VGW:", "PUREGOLD SAN FERNANDO")
            );

            Log("   [INSTALL] Installing Annual & Variance Gateway silently...");
            bool success = await RunProcessAsync(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", "Installing A&VGW");
            RecordInstallResult("A&VGW", success);

            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string? configPath = null;

            foreach (var dir in new[] { progFiles, progFilesX86 })
            {
                string targetDir = Path.Combine(dir, "Annual & Variance Gateway");
                if (Directory.Exists(targetDir))
                {
                    configPath = Path.Combine(targetDir, "config.json");
                    break;
                }
            }

            if (configPath != null && File.Exists(configPath) && !string.IsNullOrWhiteSpace(_sharedDatabaseIp))
            {
                try
                {
                    string json = File.ReadAllText(configPath);

                    json = Regex.Replace(json, @"""DbHost""\s*:\s*""[^""]*""", $"\"DbHost\": \"{_sharedDatabaseIp}\"");
                    json = Regex.Replace(json, @"""DefaultStoreNum""\s*:\s*""[^""]*""", $"\"DefaultStoreNum\": \"{storeNum}\"");
                    json = Regex.Replace(json, @"""FallbackStoreName""\s*:\s*""[^""]*""", $"\"FallbackStoreName\": \"{fallbackStore}\"");

                    File.WriteAllText(configPath, json);
                    Log("   [SUCCESS] A&VGW config.json updated with custom settings.");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to update A&VGW config: {ex.Message}");
                }
            }
            else
            {
                Log($"   [WARN] config.json not found or IP was cancelled. It may be installed in a non-standard directory.");
            }

            if (File.Exists(@"C:\Program Files (x86)\Annual & Variance Gateway\Annual And Variance Gateway.exe"))
            {
                targetExe = @"C:\Program Files (x86)\Annual & Variance Gateway\Annual And Variance Gateway.exe";
            }
            else if (File.Exists(@"C:\Program Files\Annual & Variance Gateway\Annual And Variance Gateway.exe"))
            {
                targetExe = @"C:\Program Files\Annual & Variance Gateway\Annual And Variance Gateway.exe";
            }

            await CreateAndVerifyDesktopShortcut("Annual & Variance Gateway", targetExe);
            IncrementProgress();
        }

        private async Task InstallPITK()
        {
            Log("------------------------------------------------");
            Log("   [INIT] Installing Puregold IT Toolkit (PITK)...");

            string targetExe = @"C:\Program Files (x86)\Puregold IT Toolkit\PuregoldITToolkit.exe";
            if (!File.Exists(targetExe))
            {
                string altExe = @"C:\Program Files\Puregold IT Toolkit\PuregoldITToolkit.exe";
                if (File.Exists(altExe))
                {
                    targetExe = altExe;
                }
            }

            bool isForceInstall = IsForceInstall("PITK", "Puregold IT Toolkit");
            bool isInstalled = IsAppInstalled("PITK") || IsAppInstalled("Puregold IT Toolkit") || File.Exists(targetExe);

            if (!isForceInstall && isInstalled)
            {
                Log("   [SKIP] Puregold IT Toolkit (PITK) is already installed.");
                RecordInstallResult("PITK", false, true);
                await CreateAndVerifyDesktopShortcut("Puregold IT Toolkit", targetExe);
                IncrementProgress();
                return;
            }

            string installerExe = "PITK Setup.exe";
            string? resolved = ResolveAssetPath(installerExe);
            if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved))
            {
                if (ResolveAssetPath("pitk.exe") != null)
                {
                    installerExe = "pitk.exe";
                }
            }

            string? installerPath = ResolveAssetPath(installerExe);
            bool success = false;
            if (!string.IsNullOrEmpty(installerPath) && File.Exists(installerPath))
            {
                success = await RunProcessAsync(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", "Installing PITK");
            }
            else
            {
                Log($"   [SKIP] Installer not found: {installerExe}");
            }

            RecordInstallResult("PITK", success);

            if (File.Exists(@"C:\Program Files (x86)\Puregold IT Toolkit\PuregoldITToolkit.exe"))
            {
                targetExe = @"C:\Program Files (x86)\Puregold IT Toolkit\PuregoldITToolkit.exe";
            }
            else if (File.Exists(@"C:\Program Files\Puregold IT Toolkit\PuregoldITToolkit.exe"))
            {
                targetExe = @"C:\Program Files\Puregold IT Toolkit\PuregoldITToolkit.exe";
            }

            await CreateAndVerifyDesktopShortcut("Puregold IT Toolkit", targetExe);
            IncrementProgress();
        }

        private async Task InstallWampServer()
        {
            if (!File.Exists(@"C:\wamp64\wampmanager.exe"))
            {
                await SmartInstall("WampServer 3.4", "wampserver.exe", "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", "WampServer");
            }
            else
            {
                Log("   [SKIP] WampServer appears to be installed.");
            }

            Log("   [CONFIG] Configuring WampServer Environment...");

            string wampBase = @"C:\wamp64";

            if (Directory.Exists(wampBase))
            {
                try
                {

                    string apacheRoot = Path.Combine(wampBase, @"bin\apache");
                    string? apacheVerDir = Directory.Exists(apacheRoot)
                    ? Directory.GetDirectories(apacheRoot)
                        .FirstOrDefault(d => Path.GetFileName(d).StartsWith("apache"))
                    : null;

                    if (apacheVerDir != null)
                    {
                        string vhostPath = Path.Combine(apacheVerDir, @"conf\extra\httpd-vhosts.conf");
                        if (File.Exists(vhostPath))
                        {
                            string newVhostConfig = @"
Virtual Hosts
<VirtualHost _default_:80>
ServerName localhost
ServerAlias localhost
DocumentRoot ""${INSTALL_DIR}/www/puregold""
<Directory ""${INSTALL_DIR}/www/puregold/"">
Options +Indexes +Includes +FollowSymLinks +MultiViews
AllowOverride All
Require all granted
</Directory>
</VirtualHost>";
                            File.WriteAllText(vhostPath, newVhostConfig);
                            Log("   [CONFIG] httpd-vhosts.conf updated.");
                        }
                    }

                    string phpRoot = Path.Combine(wampBase, @"bin\php");
                    string? phpVerDir = Directory.Exists(phpRoot)
                        ? Directory.GetDirectories(phpRoot)
                            .FirstOrDefault(d => Path.GetFileName(d).StartsWith("php8.3"))
                        : null;

                    if (phpVerDir != null)
                    {
                        string extDir = Path.Combine(phpVerDir, "ext");
                        string dll1 = "php_sqlsrv_83_ts_x64.dll";
                        string dll2 = "php_pdo_sqlsrv_83_ts_x64.dll";

                        string? sourceDll1 = ResolveAssetPath(dll1);
                        string? sourceDll2 = ResolveAssetPath(dll2);

                        if (!string.IsNullOrEmpty(sourceDll1) && File.Exists(sourceDll1)) File.Copy(sourceDll1, Path.Combine(extDir, dll1), true);
                        if (!string.IsNullOrEmpty(sourceDll2) && File.Exists(sourceDll2)) File.Copy(sourceDll2, Path.Combine(extDir, dll2), true);

                        string iniPath = Path.Combine(phpVerDir, "phpForApache.ini");
                        if (File.Exists(iniPath))
                        {
                            string content = File.ReadAllText(iniPath);
                            if (!content.Contains(dll1))
                            {
                                content += Environment.NewLine + $"; --- Added by PGInstaller ---" + Environment.NewLine;
                                content += $"extension={dll1}" + Environment.NewLine;
                                content += $"extension={dll2}" + Environment.NewLine;
                                File.WriteAllText(iniPath, content);
                                Log("   [CONFIG] phpForApache.ini updated with SQL drivers.");
                            }
                        }
                    }

                    string wampExe = Path.Combine(wampBase, "wampmanager.exe");
                    if (File.Exists(wampExe))
                    {
                        Log("   [START] Starting WampServer...");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = wampExe,
                            UseShellExecute = true,
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Wamp Config Failed: {ex.Message}");
                }
            }
            IncrementProgress();
        }

        private async Task PasteVARIANCE()
        {
            string? varianceZip = ResolveAssetPath("variance.zip");
            string targetDir = @"C:\wamp64\www\puregold";

            if (!string.IsNullOrEmpty(varianceZip) && File.Exists(varianceZip))
            {
                Log("   [DEPLOY] Unzipping Variance System...");
                try
                {
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                    await ExtractWithProgress(varianceZip, targetDir, CreateStepProgress("Extracting Variance"), overwrite: true);
                    Log("   [SUCCESS] Variance deployed to www/puregold.");
                }
                catch (Exception ex) { Log($"   [ERROR] Variance Deploy failed: {ex.Message}"); }
            }
            else
            {
                Log("   [WARN] variance.zip not found in Assets.");
            }
            IncrementProgress();
        }

        private bool IsNetFx3Installed()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5");
                if (key != null && Convert.ToInt32(key.GetValue("Install") ?? 0) == 1)
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        private string GetNetFxSourceFolder()
        {
            try
            {
                var osVersion = Environment.OSVersion.Version;
                int build = osVersion.Build;

                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string productName = key?.GetValue("ProductName")?.ToString() ?? "";
                bool isServer = productName.Contains("Server", StringComparison.OrdinalIgnoreCase);

                if (isServer)
                {
                    if (build >= 26100) return "Win11 24h2";
                    if (build >= 20348) return "Win10 21h2";
                    if (build >= 17763) return "Win10 1809";
                    if (build >= 14393) return "Win10 1607";
                }
                else
                {
                    string releaseId =
                        key?.GetValue("DisplayVersion")?.ToString() ??
                        key?.GetValue("ReleaseId")?.ToString() ??
                        "";

                    string osLabel = (build >= 22000) ? "Win11" : "Win10";
                    if (!string.IsNullOrEmpty(releaseId))
                    {
                        return $"{osLabel} {releaseId.ToLower()}";
                    }
                    return osLabel;
                }
                return (build >= 22000) ? "Win11" : "Win10";
            }
            catch (Exception ex)
            {
                Log($"   [WARN] OS Detection error: {ex.Message}");
                return Environment.OSVersion.Version.Build >= 22000 ? "Win11" : "Win10";
            }
        }

        private async Task InstallNetFx3()
        {
            Log("   [INIT] Checking .NET Framework 3.5 status...");

            if (IsNetFx3Installed())
            {
                Log("   [INFO] .NET Framework 3.5 is already installed on this machine.");
                IncrementProgress();
                return;
            }

            Log("   [INIT] Starting Offline .NET 3.5 Installation...");
            string? netfxZip = ResolveAssetPath("netfx.zip");
            string netfxExtractDir = @"C:\Assets\NetFX3_Source";

            if (string.IsNullOrEmpty(netfxZip) || !File.Exists(netfxZip))
            {
                await ExtractSpecificFile(null, "*netfx*");
                netfxZip = ResolveAssetPath("netfx.zip");
            }

            if (!string.IsNullOrEmpty(netfxZip) && File.Exists(netfxZip) && !Directory.Exists(netfxExtractDir))
            {
                Log("   [EXTRACT] Unzipping NetFX3 sources...");
                try
                {
                    Directory.CreateDirectory(netfxExtractDir);
                    await ExtractWithProgress(netfxZip, netfxExtractDir, CreateStepProgress("Extracting NetFX3"));
                }
                catch (Exception ex)
                {
                    Log($"   [WARN] Extraction failed: {ex.Message}");
                }
            }

            string? sourcePath = null;
            if (Directory.Exists(netfxExtractDir))
            {
                string matchedFolder = GetNetFxSourceFolder();
                if (!string.IsNullOrEmpty(matchedFolder) && Directory.Exists(Path.Combine(netfxExtractDir, matchedFolder)))
                {
                    sourcePath = Path.Combine(netfxExtractDir, matchedFolder);
                }
                else if (!string.IsNullOrEmpty(matchedFolder))
                {
                    string osPrefix = matchedFolder.Split(' ')[0];
                    sourcePath = Directory.GetDirectories(netfxExtractDir, osPrefix + "*", SearchOption.AllDirectories).FirstOrDefault();
                }

                if (string.IsNullOrEmpty(sourcePath))
                {
                    var cab = Directory.GetFiles(netfxExtractDir, "*.cab", SearchOption.AllDirectories).FirstOrDefault();
                    if (cab != null)
                    {
                        sourcePath = Path.GetDirectoryName(cab);
                    }
                    else
                    {
                        sourcePath = Directory.GetDirectories(netfxExtractDir, "*sxs*", SearchOption.AllDirectories).FirstOrDefault()
                                     ?? Directory.GetDirectories(netfxExtractDir).FirstOrDefault();
                    }
                }

                if (!string.IsNullOrEmpty(sourcePath) && Directory.Exists(Path.Combine(sourcePath, "sxs")))
                {
                    sourcePath = Path.Combine(sourcePath, "sxs");
                }
            }

            bool success = false;

            // 1. Try offline DISM with source
            if (!string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath))
            {
                Log($"   [INSTALL] Installing from offline source: {Path.GetFileName(sourcePath)}");
                success = await RunProcessAsync(
                    "dism",
                    $"/Online /Enable-Feature /FeatureName:NetFx3 /All /Source:\"{sourcePath}\" /LimitAccess /NoRestart",
                    "Enabling .NET 3.5 (Offline)"
                );

                if (!success)
                {
                    Log("   [RETRY] Retrying offline source without LimitAccess...");
                    success = await RunProcessAsync(
                        "dism",
                        $"/Online /Enable-Feature /FeatureName:NetFx3 /All /Source:\"{sourcePath}\" /NoRestart",
                        "Enabling .NET 3.5 (Offline Fallback)"
                    );
                }
            }

            // 2. Direct CAB package fallback if present
            if (!success)
            {
                string? standaloneCab = ResolveAssetPath("netfx3.cab")
                    ?? (Directory.Exists(netfxExtractDir) ? Directory.GetFiles(netfxExtractDir, "*netfx3*.cab", SearchOption.AllDirectories).FirstOrDefault() : null);

                if (!string.IsNullOrEmpty(standaloneCab) && File.Exists(standaloneCab))
                {
                    Log($"   [FALLBACK] Installing direct CAB package: {Path.GetFileName(standaloneCab)}...");
                    success = await RunProcessAsync("dism", $"/Online /Add-Package /PackagePath:\"{standaloneCab}\" /NoRestart", "Installing .NET 3.5 (CAB)");
                }
            }

            // 3. Online DISM enable fallback
            if (!success && !IsNetFx3Installed())
            {
                Log("   [FALLBACK] Attempting Windows DISM online feature enable...");
                success = await RunProcessAsync("dism", "/Online /Enable-Feature /FeatureName:NetFx3 /All /NoRestart", "Enabling .NET 3.5 (Online)");
            }

            // 4. PowerShell fallback
            if (!success && !IsNetFx3Installed())
            {
                Log("   [FALLBACK] Attempting PowerShell Enable-WindowsOptionalFeature...");
                success = await RunProcessAsync("powershell", "-NoProfile -Command \"Enable-WindowsOptionalFeature -Online -FeatureName NetFx3 -All -NoRestart\"", "Enabling .NET 3.5 (PowerShell)");
            }

            if (success || IsNetFx3Installed())
            {
                Log("   [SUCCESS] .NET Framework 3.5 installed and active.");
            }
            else
            {
                Log("   [ERROR] .NET Framework 3.5 installation could not be completed.");
            }

            IncrementProgress();
        }

        private async Task InstallPIMS()
        {
            await InstallNetFx3();

            string? pimsZip = ResolveAssetPath("pims.zip");
            string pimsRoot = @"C:\Assets\PG_PIMS_Install";

            if (string.IsNullOrEmpty(pimsZip) || !File.Exists(pimsZip))
            {
                Log("   [ERROR] pims.zip not found in Assets.");
                return;
            }

            if (!Directory.Exists(pimsRoot))
            {
                Log("   [INIT] Extracting pims.zip...");
                try
                {
                    Directory.CreateDirectory(pimsRoot);
                    await ExtractWithProgress(pimsZip, pimsRoot, CreateStepProgress("Extracting PIMS"));
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Extraction failed: {ex.Message}");
                    return;
                }
            }

            try
            {
                using (var sessionMgr = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager", true))
                {
                    sessionMgr?.DeleteValue("PendingFileRenameOperations", false);
                }
                using var wu = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update", true);
                wu?.DeleteSubKey("RebootRequired", false);
            }
            catch (Exception ex)
            {
                Log($"   [WARN] Could not clear reboot flags: {ex.Message}");
            }

            string crDir = Path.Combine(pimsRoot, "CR10");
            string crMsi = Path.Combine(crDir, "scrent.msi");

            if (VerifyFile(crMsi))
            {
                string crKey = "AV860-010S000-00000YV";
                string crArgs = $"/i \"{crMsi}\" PIDKEY=\"{crKey}\" ROOTDRIVE=\"C:\\\" /qn /norestart";
                await RunProcessAsync("msiexec.exe", crArgs, "Installing Crystal Reports 10 Enterprise");
            }
            else
            {
                Log($"   [WARN] scrent.msi not found at {crMsi}");
            }

            string crRedist86 = Path.Combine(pimsRoot, "CRRedist2005_x86.msi");
            if (VerifyFile(crRedist86))
            {
                await RunProcessAsync("msiexec.exe", $"/i \"{crRedist86}\" /qn /norestart", "Installing CR Redist 2005 (x86)");
            }

            if (Environment.Is64BitOperatingSystem)
            {
                string crRedist64 = Path.Combine(pimsRoot, "CRRedist2005_X64.msi");
                if (VerifyFile(crRedist64))
                {
                    await RunProcessAsync("msiexec.exe", $"/i \"{crRedist64}\" /qn /norestart", "Installing CR Redist 2005 (x64)");
                }
            }

            string poMsi = Path.Combine(pimsRoot, "POTracking", "POTracking.msi");
            if (VerifyFile(poMsi))
            {
                await RunProcessAsync("msiexec.exe", $"/i \"{poMsi}\" ALLUSERS=1 /qn /norestart", "Installing POTracking");
            }

            string sqlDir = Path.Combine(pimsRoot, "SQLServer2005");
            string sqlMsi = Environment.Is64BitOperatingSystem
                ? Path.Combine(sqlDir, "SQLServer2005_BC_x64.msi")
                : Path.Combine(sqlDir, "SQLServer2005_BC.msi");

            if (VerifyFile(sqlMsi))
            {
                await RunProcessAsync("msiexec.exe", $"/i \"{sqlMsi}\" ALLUSERS=1 /qn /norestart", "Installing SQL Server 2005 BC");
            }

            string fmsSource = Path.Combine(pimsRoot, "FMS");
            string fmsDest = @"C:\FMS";

            if (Directory.Exists(fmsSource))
            {
                Log("   [COPY] Deploying FMS to C:\\FMS...");
                await CopyDirectoryAsync(fmsSource, fmsDest);
                string pimsExe = Path.Combine(fmsDest, "pims.exe");
                if (File.Exists(pimsExe))
                {
                    await CreateAllUsersShortcut("PIMS", pimsExe);
                    Log("   [SHORTCUT] Created Desktop Shortcut: PIMS");
                }
                else
                {
                    Log("   [WARN] pims.exe not found in C:\\FMS after copy.");
                }
            }
            else
            {
                Log($"   [ERROR] Source FMS folder not found at {fmsSource}");
            }

            Log("   [CONFIG] Requesting Server IP Address...");

            if (string.IsNullOrEmpty(_sharedDatabaseIp))
            {
                _sharedDatabaseIp = await Application.Current.Dispatcher.InvokeAsync(() =>
                    ShowInputDialog("Enter Server IP Address config for FIDB, FIHO (PIMS), and A&VGW:", "192.92.1.100")
                );
            }

            if (!string.IsNullOrWhiteSpace(_sharedDatabaseIp))
            {
                try
                {
                    ConfigurePimsRegistry("FIHO", _sharedDatabaseIp);
                    ConfigurePimsRegistry("FIDB", _sharedDatabaseIp);
                    Log("   [SUCCESS] PIMS Registry Configuration Applied.");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Registry Write Failed: {ex.Message}. (Run as Admin?)");
                }
            }
            else
            {
                Log("   [WARN] IP Address input cancelled. Registry not updated.");
            }

            IncrementProgress();
        }

        private bool VerifyFile(string path)
        {
            if (File.Exists(path))
                return true;
            Log($"   [ERROR] Missing file: {Path.GetFileName(path)}");
            return false;

        }

        private async Task CopyDirectoryAsync(string sourceDir, string targetDir)
        {
            try
            {
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(targetDir);
                    string sourceDirClean = sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = file[sourceDirClean.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        string destPath = Path.Combine(targetDir, relativePath);

                        string destDirPath = Path.GetDirectoryName(destPath)!;
                        if (!string.IsNullOrEmpty(destDirPath))
                        {
                            Directory.CreateDirectory(destDirPath);
                        }
                        File.Copy(file, destPath, true);
                    }
                });
                Log("   [SUCCESS] FMS Copied successfully.");
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Copy failed: {ex.Message}");
            }
        }

        private void ConfigurePimsRegistry(string softwareKey, string ip)
        {
            string baseKey = $@"SOFTWARE\{softwareKey}\Config";

            using var key = Registry.LocalMachine.CreateSubKey(baseKey);
            if (key != null)
            {
                key.SetValue("IPADDRESS", ip);
                key.SetValue("DATABASE", softwareKey == "FIHO" ? "FreeItemsDB" : "FREEITEMSDB");
                key.SetValue("USERNAME", "sa");
                key.SetValue("PASSWORD", "sa");
                key.SetValue("PROVIDER", "SQLOLEDB.1");
                key.SetValue("Persist Security Info", "False");
            }
        }

        private string ShowInputDialog(string question, string defaultAnswer = "")
        {
            var dialog = new InputDialog(question, defaultAnswer)
            {
                Owner = Application.Current.MainWindow 
            };

            bool? result = dialog.ShowDialog();
            return result == true ? dialog.Answer : "";
        }

        private async Task InstallFSDM()
        {
            string? fsdmZip = ResolveAssetPath("FSDM.zip");
            string tempFsdmRoot = @"C:\Assets\PG_FSDM_Install";

            if (Directory.Exists(tempFsdmRoot))
                try { Directory.Delete(tempFsdmRoot, true); } catch { }

            if (string.IsNullOrEmpty(fsdmZip) || !File.Exists(fsdmZip))
            {
                Log("   [ERROR] FSDM.zip not found in Assets.");
                return;
            }

            Log("   [INIT] Extracting FSDM.zip...");
            try
            {
                Directory.CreateDirectory(tempFsdmRoot);
                await ExtractWithProgress(fsdmZip, tempFsdmRoot, CreateStepProgress("Extracting FSDM"));
            }
            catch (Exception ex) { Log($"   [ERROR] Extraction failed: {ex.Message}"); return; }

            string ssce86 = Path.Combine(tempFsdmRoot, "SSCERuntime_x86-ENU.msi");
            string ssce64 = Path.Combine(tempFsdmRoot, "SSCERuntime_x64-ENU.msi");

            if (File.Exists(ssce86))
                await RunProcessAsync("msiexec.exe", $"/i \"{ssce86}\" /quiet", "Installing SSCE Runtime x86");

            if (File.Exists(ssce64))
                await RunProcessAsync("msiexec.exe", $"/i \"{ssce64}\" /quiet", "Installing SSCE Runtime x64");

            string fsDevZip = Path.Combine(tempFsdmRoot, "FSDevMan.zip");
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string fsDestDir = Path.Combine(progFiles, "FSDevMan");

            if (File.Exists(fsDevZip))
            {
                Log("   [EXTRACT] Installing FSDevMan to Program Files...");
                if (!Directory.Exists(fsDestDir)) Directory.CreateDirectory(fsDestDir);

                try
                {
                    await ExtractWithProgress(fsDevZip, fsDestDir, CreateStepProgress("Extracting FSDevMan"));
                }
                catch (Exception ex) { Log($"   [WARN] FSDevMan extract issue: {ex.Message}"); }

                string exePath = Path.Combine(fsDestDir, "FSDeviceManager.exe");

                if (File.Exists(exePath))
                {
                    await CreateAllUsersShortcut("FSDM", exePath, fsDestDir);
                    Log("   [SHORTCUT] Created Desktop Shortcut: FSDM");
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
                        if (key != null)
                        {
                            key.SetValue(exePath, "~ RUNASADMIN");
                            Log("   [CONFIG] Applied 'Run as Administrator' flag to FSDeviceManager.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"   [WARN] Failed to set Admin flag (Requires Admin rights to write to HKLM): {ex.Message}");
                    }
                }
                else
                {
                    Log($"   [ERROR] FSDeviceManager.exe not found at {exePath}");
                }
            }

            string sdkZip = Path.Combine(tempFsdmRoot, "SDK.zip");
            string sdkTempDir = Path.Combine(tempFsdmRoot, "SDK_Temp");

            if (File.Exists(sdkZip))
            {
                Log("   [EXTRACT] Preparing SDK Installer...");
                try
                {
                    Directory.CreateDirectory(sdkTempDir);
                    await ExtractWithProgress(sdkZip, sdkTempDir, CreateStepProgress("Extracting SDK"));

                    string regBat = Path.Combine(sdkTempDir, "Register_SDK_x64.bat");

                    if (File.Exists(regBat))
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c \"{regBat}\"",
                            WorkingDirectory = sdkTempDir,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        await RunCustomProcess(startInfo, "Registering SDK (Copying to System32)");
                    }
                    else
                    {
                        Log("   [ERROR] Register_SDK_x64.bat not found in SDK zip.");
                    }
                }
                catch (Exception ex) { Log($"   [ERROR] SDK Installation failed: {ex.Message}"); }
            }

            string dbUpdater = "FSDM Database Updater.exe";
            string dbSource = Path.Combine(tempFsdmRoot, dbUpdater);

            if (File.Exists(dbSource))
            {
                string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string dbDest = Path.Combine(publicDesktop, dbUpdater);
                try
                {
                    File.Copy(dbSource, dbDest, true);
                    Log("   [COPY] FSDM Database Updater copied to Desktop.");
                }
                catch (Exception ex) { Log($"   [ERROR] Failed copy updater: {ex.Message}"); }
            }
            IncrementProgress();
        }

        private async Task InstallCorelPSIllu(IEnumerable<string> selectedApps)
        {
            await InstallNetFx3();


            bool installX5 = selectedApps.Contains("Coreldraw Graphics X5");
            bool installX7 = selectedApps.Contains("Coreldraw Graphics X7");

            if (installX5)
            {
                string corelX5Exe = "crdx5.exe";
                string? corelX5Path = ResolveAssetPath(corelX5Exe);

                if (!string.IsNullOrEmpty(corelX5Path) && File.Exists(corelX5Path))
                {
                    await RunProcessAsync(corelX5Path, "", "Launching CorelDRAW X5 Installer");

                    string prog86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    string installedPathX5 = Path.Combine(prog86, @"Corel\CorelDRAW Graphics Suite X5\Programs\CorelDRW.exe");

                    if (!File.Exists(installedPathX5))
                    {
                        string prog64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                        installedPathX5 = Path.Combine(prog64, @"Corel\CorelDRAW Graphics Suite X5\Programs\CorelDRW.exe");
                    }

                    if (File.Exists(installedPathX5))
                    {
                        await CreateAllUsersShortcut("CorelDRAW X5", installedPathX5);
                        Log("   [SHORTCUT] Created All Users Shortcut: CorelDRAW X5");
                    }
                    else
                    {
                        Log("   [WARN] Could not locate CorelDRW.exe for X5 to create shortcut.");
                    }
                }
                else
                {
                    Log($"   [SKIP] CorelDRAW X5 installer not found: {corelX5Exe}");
                }
            }

            if (installX7)
            {
                string corelX7Exe = "crdx7.exe";
                string? corelX7Path = ResolveAssetPath(corelX7Exe);

                if (!string.IsNullOrEmpty(corelX7Path) && File.Exists(corelX7Path))
                {
                    await RunProcessAsync(corelX7Path, "", "Launching CorelDRAW X7 Installer");

                    string prog86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    string installedPathX7 = Path.Combine(prog86, @"Corel\CorelDRAW Graphics Suite X7\Programs\CorelDRW.exe");

                    if (!File.Exists(installedPathX7))
                    {
                        string prog64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                        installedPathX7 = Path.Combine(prog64, @"Corel\CorelDRAW Graphics Suite X7\Programs\CorelDRW.exe");
                    }

                    if (File.Exists(installedPathX7))
                    {
                        await CreateAllUsersShortcut("CorelDRAW X7", installedPathX7);
                        Log("   [SHORTCUT] Created All Users Shortcut: CorelDRAW X7");
                    }
                    else
                    {
                        Log("   [WARN] Could not locate CorelDRW.exe for X7 to create shortcut.");
                    }
                }
                else
                {
                    Log($"   [SKIP] CorelDRAW X7 installer not found: {corelX7Exe}");
                }
            }

            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string? illuZip = ResolveAssetPath("illucs6.zip");

            if (!string.IsNullOrEmpty(illuZip) && File.Exists(illuZip))
            {
                string destDir = Path.Combine(progFiles, "IllustratorCS6Portable");
                string exePath = Path.Combine(destDir, "IllustratorCS6Portable.exe");

                if (!Directory.Exists(destDir))
                {
                    Log("   [EXTRACT] Unzipping Illustrator CS6 to Program Files...");
                    try
                    {
                        Directory.CreateDirectory(destDir);
                        await ExtractWithProgress(illuZip, destDir, CreateStepProgress("Extracting Illustrator CS6"));
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Illustrator extraction failed: {ex.Message}");
                    }
                }

                if (File.Exists(exePath))
                {
                    await CreateAllUsersShortcut("Illustrator CS6", exePath, destDir);
                    Log("   [SHORTCUT] Created All Users Shortcut: Illustrator CS6");
                }
            }
            else
            {
                Log("   [SKIP] illucs6.zip not found.");
            }

            string? psZip = ResolveAssetPath("pscs6.zip");

            if (!string.IsNullOrEmpty(psZip) && File.Exists(psZip))
            {
                string destDir = Path.Combine(progFiles, "PhotoshopCS6Portable");
                string exePath = Path.Combine(destDir, "PhotoshopCS6Portable.exe");

                if (!Directory.Exists(destDir))
                {
                    Log("   [EXTRACT] Unzipping Photoshop CS6 to Program Files...");
                    try
                    {
                        Directory.CreateDirectory(destDir);
                        await ExtractWithProgress(psZip, destDir, CreateStepProgress("Extracting Photoshop CS6"));
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Photoshop extraction failed: {ex.Message}");
                    }
                }

                if (File.Exists(exePath))
                {
                    await CreateAllUsersShortcut("Photoshop CS6", exePath, destDir);
                    Log("   [SHORTCUT] Created All Users Shortcut: Photoshop CS6");
                }
            }
            else
            {
                Log("   [SKIP] pscs6.zip not found.");
            }

            IncrementProgress();
        }

        private async Task<bool> CreateAndVerifyDesktopShortcut(string linkName, string targetExePath, string? workingDir = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(workingDir) && !string.IsNullOrWhiteSpace(targetExePath))
                {
                    workingDir = Path.GetDirectoryName(targetExePath);
                }

                string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string publicLnk = Path.Combine(publicDesktop, $"{linkName}.lnk");
                string userLnk = Path.Combine(userDesktop, $"{linkName}.lnk");

                string publicStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "PG Store Apps");
                if (!Directory.Exists(publicStartMenu))
                {
                    Directory.CreateDirectory(publicStartMenu);
                }
                string startMenuLnk = Path.Combine(publicStartMenu, $"{linkName}.lnk");

                Log($"   [SHORTCUT] Creating desktop shortcut for '{linkName}'...");

                string cleanTarget = (targetExePath ?? "").Replace("'", "''");
                string cleanWorkDir = (workingDir ?? "").Replace("'", "''");
                string workDirSnippet = string.IsNullOrEmpty(cleanWorkDir) ? "" : $"$s.WorkingDirectory = '{cleanWorkDir}';";
                string workDirSnippet2 = string.IsNullOrEmpty(cleanWorkDir) ? "" : $"$s2.WorkingDirectory = '{cleanWorkDir}';";

                string script = $"$ws = New-Object -ComObject WScript.Shell; " +
                                $"$s = $ws.CreateShortcut('{publicLnk.Replace("'", "''")}'); " +
                                $"$s.TargetPath = '{cleanTarget}'; {workDirSnippet} $s.Save(); " +
                                $"$s2 = $ws.CreateShortcut('{startMenuLnk.Replace("'", "''")}'); " +
                                $"$s2.TargetPath = '{cleanTarget}'; {workDirSnippet2} $s2.Save();";

                await RunProcessAsync("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"", $"Creating Shortcut: {linkName}", true);

                await Task.Delay(500);

                bool exeExists = !string.IsNullOrEmpty(targetExePath) && File.Exists(targetExePath);
                if (exeExists)
                {
                    Log($"   [VERIFY] Target executable verified: {targetExePath}");
                }
                else
                {
                    Log($"   [WARN] Target executable not found at: {targetExePath}");
                }

                bool shortcutExists = File.Exists(publicLnk) || File.Exists(userLnk) || File.Exists(startMenuLnk);
                if (shortcutExists)
                {
                    Log($"   [SUCCESS] Verified: Desktop shortcut for '{linkName}' created successfully at '{publicLnk}'.");
                    return true;
                }
                else
                {
                    Log($"   [ERROR] Verification failed: Desktop shortcut for '{linkName}' was not found.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Failed to create or verify shortcut for '{linkName}': {ex.Message}");
                return false;
            }
        }

        private async Task CreateAllUsersShortcut(string linkName, string targetPath, string? workingDir = null)
        {
            await CreateAndVerifyDesktopShortcut(linkName, targetPath, workingDir);
        }

        private async Task InstallPutty()
        {
            await SmartInstall("PuTTY", "putty.msi", "/qn", "PuTTY");
            int posCount = await GetOrPromptPosCountAsync();
            await GeneratePOSConfigurations(posCount);
            IncrementProgress();
        }

        private async Task InstallRadminViewer()
        {
            await SmartInstall("Radmin Viewer", "radminv.msi", "/qn /norestart", "Radmin Viewer");
            IncrementProgress();
        }

        private async Task InstallWinSCP()
        {
            await SmartInstall(
                 "WinSCP",
                 "WinSCP.exe",
                 "/VERYSILENT /NORESTART /ALLUSERS",
                 "WinSCP"
            );

            int posCount = await GetOrPromptPosCountAsync();
            await GeneratePOSConfigurations(posCount);
            IncrementProgress();
        }

        private string GetSubnetPrefix(string? ip = null)
        {
            string sourceIp = !string.IsNullOrWhiteSpace(ip)
                ? ip
                : (!string.IsNullOrWhiteSpace(_consoIpInput) ? _consoIpInput : TargetIp);

            if (!string.IsNullOrWhiteSpace(sourceIp))
            {
                string clean = sourceIp.Trim();
                if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
                if (clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(8);
                int slashIdx = clean.IndexOf('/');
                if (slashIdx >= 0) clean = clean.Substring(0, slashIdx);
                int colonIdx = clean.IndexOf(':');
                if (colonIdx >= 0) clean = clean.Substring(0, colonIdx);

                var parts = clean.Split('.');
                if (parts.Length >= 3 && parts.Take(3).All(p => int.TryParse(p, out int n) && n >= 0 && n <= 255))
                {
                    return $"{parts[0]}.{parts[1]}.{parts[2]}";
                }
            }
            return "192.168.1";
        }

        private async Task GeneratePOSConfigurations(int posCount)
        {
            string storeIp = await GetOrPromptConsoIpAsync();
            string subnet = GetSubnetPrefix(storeIp);
            string consoIp = $"{subnet}.50";

            Log($"   [CONFIG] Generating dynamic POS configurations for {posCount} terminals (Subnet: {subnet}, Conso: {consoIp})...");

            // --- 1. Dynamic WinSCP.ini & Registry Generation ---
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[Configuration\\Security]");
                sb.AppendLine("UseMasterPassword=0");
                sb.AppendLine();
                sb.AppendLine("[Configuration\\Interface]");
                sb.AppendLine("Interface=1");
                sb.AppendLine();

                // Conso Server session
                sb.AppendLine("[Sessions\\Conso%20Server]");
                sb.AppendLine($"HostName={consoIp}");
                sb.AppendLine("PortNumber=22");
                sb.AppendLine("FSProtocol=0");
                sb.AppendLine("UserName=");
                sb.AppendLine();

                // Zone 11 session
                sb.AppendLine("[Sessions\\Zone%2011]");
                sb.AppendLine($"HostName={consoIp}");
                sb.AppendLine("PortNumber=22");
                sb.AppendLine("FSProtocol=0");
                sb.AppendLine("UserName=");
                sb.AppendLine();

                // POS 1 to N sessions
                for (int i = 1; i <= posCount; i++)
                {
                    string posIp = $"{subnet}.{50 + i}";
                    sb.AppendLine($"[Sessions\\POS%20{i}]");
                    sb.AppendLine($"HostName={posIp}");
                    sb.AppendLine("PortNumber=22");
                    sb.AppendLine("FSProtocol=0");
                    sb.AppendLine("UserName=");
                    sb.AppendLine();
                }

                string iniContent = sb.ToString();
                string[] winScpDirs =
                [
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinSCP"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinSCP"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinSCP")
                ];

                bool written = false;
                foreach (string dir in winScpDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        try
                        {
                            string destFile = Path.Combine(dir, "WinSCP.ini");
                            await File.WriteAllTextAsync(destFile, iniContent);
                            Log($"   [CONFIG] Dynamic WinSCP.ini applied to: {destFile}");
                            written = true;
                        }
                        catch (Exception ex)
                        {
                            Log($"   [ERROR] Failed to write WinSCP.ini in {dir}: {ex.Message}");
                        }
                    }
                }

                if (!written)
                {
                    string fallbackDir = winScpDirs[0];
                    try
                    {
                        Directory.CreateDirectory(fallbackDir);
                        string destFile = Path.Combine(fallbackDir, "WinSCP.ini");
                        await File.WriteAllTextAsync(destFile, iniContent);
                        Log($"   [CONFIG] WinSCP directory created and dynamic WinSCP.ini applied to: {destFile}");
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Failed to create WinSCP folder/ini: {ex.Message}");
                    }
                }

                // Also write to Windows Registry for WinSCP so sessions are available in Registry mode
                try
                {
                    const string winscpSessionsKey = @"Software\Martin Prikryl\WinSCP 2\Sessions";

                    void SaveWinScpSession(string sessionName, string host, int port)
                    {
                        string encodedSession = Uri.EscapeDataString(sessionName).Replace("+", "%20");
                        using var key = Registry.CurrentUser.CreateSubKey($@"{winscpSessionsKey}\{encodedSession}");
                        if (key != null)
                        {
                            key.SetValue("HostName", host, RegistryValueKind.String);
                            key.SetValue("PortNumber", port, RegistryValueKind.DWord);
                            key.SetValue("FSProtocol", 0, RegistryValueKind.DWord);
                            key.SetValue("UserName", "", RegistryValueKind.String);
                        }
                    }

                    SaveWinScpSession("Zone 11", consoIp, 22);
                    SaveWinScpSession("Conso Server", consoIp, 22);

                    for (int i = 1; i <= posCount; i++)
                    {
                        string posIp = $"{subnet}.{50 + i}";
                        SaveWinScpSession($"POS {i}", posIp, 22);
                    }

                    Log($"   [SUCCESS] WinSCP sessions registered in Windows Registry: Zone 11, Conso Server, and POS 1..{posCount}.");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to save WinSCP sessions to registry: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] WinSCP configuration generation error: {ex.Message}");
            }

            // --- 2. Dynamic PuTTY Sessions / Registry Generation ---
            try
            {
                var forwardings = new List<string>
                {
                    $"L10000={consoIp}:80",
                    $"L5900={consoIp}:5900",
                    $"L5984={consoIp}:5984"
                };

                for (int i = 1; i <= posCount; i++)
                {
                    string posIp = $"{subnet}.{50 + i}";
                    forwardings.Add($"L{10000 + i}={posIp}:80");
                    forwardings.Add($"L{5900 + i}={posIp}:5900");
                    forwardings.Add($"L{2200 + i}={posIp}:22");
                }

                string portForwardingStr = string.Join(",", forwardings);
                const string puttySessionsKey = @"Software\SimonTatham\PuTTY\Sessions";

                void SaveSession(string sessionName, string host, int port, string? pf = null)
                {
                    string encodedSession = Uri.EscapeDataString(sessionName).Replace("+", "%20");
                    using var key = Registry.CurrentUser.CreateSubKey($@"{puttySessionsKey}\{encodedSession}");
                    if (key != null)
                    {
                        key.SetValue("HostName", host, RegistryValueKind.String);
                        key.SetValue("PortNumber", port, RegistryValueKind.DWord);
                        key.SetValue("Protocol", "ssh", RegistryValueKind.String);
                        key.SetValue("CloseOnExit", 1, RegistryValueKind.DWord);
                        if (!string.IsNullOrEmpty(pf))
                        {
                            key.SetValue("PortForwardings", pf, RegistryValueKind.String);
                        }
                    }
                }

                SaveSession("Zone 11", consoIp, 22, portForwardingStr);
                SaveSession("Conso Server", consoIp, 22);

                for (int i = 1; i <= posCount; i++)
                {
                    string posIp = $"{subnet}.{50 + i}";
                    SaveSession($"POS {i}", posIp, 22);
                }

                Log($"   [SUCCESS] PuTTY sessions generated: Zone 11 (with {forwardings.Count} port forwardings), Conso Server ({consoIp}), and POS 1..{posCount}.");
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] PuTTY configuration generation error: {ex.Message}");
            }
        }

        private async Task RunChromeBookmarkScript()
        {
            Log("------------------------------------------------");
            Log("   [INIT] Configuring Chrome Bookmarks (CBM)...");

            string ownIp = await GetOrPromptConsoIpAsync();

            if (string.IsNullOrWhiteSpace(ownIp))
            {
                Log("   [SKIP] IP missing. Chrome Bookmarks configuration skipped.");
                IncrementProgress();
                return;
            }

            string cleanIp = ownIp.Trim();
            if (cleanIp.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) cleanIp = cleanIp.Substring(7);
            if (cleanIp.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) cleanIp = cleanIp.Substring(8);
            int slashIdx = cleanIp.IndexOf('/');
            if (slashIdx >= 0) cleanIp = cleanIp.Substring(0, slashIdx);
            int colonIdx = cleanIp.IndexOf(':');
            if (colonIdx >= 0) cleanIp = cleanIp.Substring(0, colonIdx);

            string scriptName = "cbm.ps1";
            string tempDir = @"C:\Assets\PG_CBM_Exec";

            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                // IMPORTANT: Always use the application's bundled cbm.ps1 or embedded script.
                // Do NOT use ResolveAssetPath which can pick up stale/broken scripts in C:\Assets!
                string appBaseScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, scriptName);
                string targetScriptPath = Path.Combine(tempDir, scriptName);

                if (File.Exists(appBaseScript))
                {
                    File.Copy(appBaseScript, targetScriptPath, true);
                }
                else
                {
                    await File.WriteAllTextAsync(targetScriptPath, GetEmbeddedCbmScript());
                }

                // Also update C:\Assets\cbm.ps1 if C:\Assets exists so any external runner is also fixed
                if (Directory.Exists(@"C:\Assets"))
                {
                    try
                    {
                        File.Copy(targetScriptPath, @"C:\Assets\cbm.ps1", true);
                    }
                    catch { }
                }

                Log($"   [EXEC] Running CBM Script with IP: {cleanIp}...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{targetScriptPath}\" -OwnIP \"{cleanIp}\" -Department \"{SelectedDepartment}\"",
                    WorkingDirectory = tempDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                await RunCustomProcess(startInfo, "Chrome Bookmark Configuration");
                Log("   [SUCCESS] CBM Script completed.");
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] CBM Execution Failed: {ex.Message}");
            }
            IncrementProgress();
        }

        private static string GetEmbeddedCbmScript()
        {
            return """
param(
    [Parameter(Mandatory=$false)]
    [string]$OwnIP = "192.168.1.101",

    [Parameter(Mandatory=$false)]
    [string]$Department = "IT"
)

$ChromeProcessName = "chrome"

Write-Host "Closing Chrome..." -ForegroundColor Yellow
Stop-Process -Name $ChromeProcessName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Clean OwnIP for Purepos Conso URL
$cleanIp = $OwnIP.Trim().TrimEnd('/')
if ($cleanIp.StartsWith("http://", [System.StringComparison]::OrdinalIgnoreCase)) {
    $cleanIp = $cleanIp.Substring(7)
} elseif ($cleanIp.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)) {
    $cleanIp = $cleanIp.Substring(8)
}
if ($cleanIp.EndsWith("/purepos_conso/login", [System.StringComparison]::OrdinalIgnoreCase)) {
    $consoUrl = "http://$cleanIp"
} else {
    $consoUrl = "http://$cleanIp/purepos_conso/login"
}

# Construct Final Bookmark Structure (v3.0 - Shelftag, TPLinux-Kiosk, IT_Tools, Conso List folders removed)
$NewChildren = @(
    @{
        date_added = "13300000000000000"
        id         = "1500"
        name       = "Purepos Conso"
        type       = "url"
        url        = $consoUrl
    },
    @{
        date_added = "13300000000000000"
        id         = "1601"
        name       = "My Portal"
        type       = "url"
        url        = "http://myportal.puregold.local/index.php/login"
    },
    @{
        date_added = "13300000000000000"
        id         = "1602"
        name       = "PCFPROv2"
        type       = "url"
        url        = "http://pcfpro_v2_test.puregold.local/"
    }
)

$localAppData = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)
$chromeBase = Join-Path $localAppData "Google\Chrome\User Data"

$chromeProfiles = @()
$defaultDir = Join-Path $chromeBase "Default"
if (-not (Test-Path $defaultDir)) {
    New-Item -ItemType Directory -Force -Path $defaultDir | Out-Null
}
$chromeProfiles += $defaultDir

if (Test-Path $chromeBase) {
    $otherProfiles = Get-ChildItem -Path $chromeBase -Directory -Filter "Profile *" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
    if ($otherProfiles) {
        $chromeProfiles += $otherProfiles
    }
}

foreach ($profileDir in ($chromeProfiles | Select-Object -Unique)) {
    $BookmarkFile = Join-Path $profileDir "Bookmarks"
    $PreferencesFile = Join-Path $profileDir "Preferences"

    if (-not (Test-Path $BookmarkFile)) {
        Write-Host "Creating new Bookmark database in $profileDir..." -ForegroundColor Cyan
        $Json = [PSCustomObject]@{
            checksum = ""
            roots = [PSCustomObject]@{
                bookmark_bar = [PSCustomObject]@{ children = @(); id = "1"; name = "Bookmarks Bar"; type = "folder" }
                other        = [PSCustomObject]@{ children = @(); id = "2"; name = "Other Bookmarks"; type = "folder" }
                synced       = [PSCustomObject]@{ children = @(); id = "3"; name = "Mobile Bookmarks"; type = "folder" }
            }
            version = 1
        }
    } else {
        Write-Host "Updating existing Bookmarks in $profileDir..." -ForegroundColor Cyan
        try {
            $Json = Get-Content $BookmarkFile -Raw -Encoding UTF8 | ConvertFrom-Json
        } catch {
            $Json = $null
        }
        if (-not $Json) {
            $Json = [PSCustomObject]@{
                checksum = ""
                roots = [PSCustomObject]@{
                    bookmark_bar = [PSCustomObject]@{ children = @(); id = "1"; name = "Bookmarks Bar"; type = "folder" }
                    other        = [PSCustomObject]@{ children = @(); id = "2"; name = "Other Bookmarks"; type = "folder" }
                    synced       = [PSCustomObject]@{ children = @(); id = "3"; name = "Mobile Bookmarks"; type = "folder" }
                }
                version = 1
            }
        }
    }

    if (-not $Json.roots) {
        $Json | Add-Member -MemberType NoteProperty -Name "roots" -Value ([PSCustomObject]@{})
    }
    if (-not $Json.roots.bookmark_bar) {
        $Json.roots | Add-Member -MemberType NoteProperty -Name "bookmark_bar" -Value ([PSCustomObject]@{ children = @(); id = "1"; name = "Bookmarks Bar"; type = "folder" })
    }

    # Clean existing matching children to avoid duplicates on re-run
    $targetFolderNames = @("Shelftag", "TPLinux-Kiosk", "IT_Tools", "Conso List", "Purepos Conso", "My Portal", "PCFPROv2", "Local Conso", "PurePOS HQ", "PCFPRO")
    $keptChildren = @()
    if ($Json.roots.bookmark_bar.children) {
        foreach ($c in $Json.roots.bookmark_bar.children) {
            if ($targetFolderNames -notcontains $c.name) {
                $keptChildren += $c
            }
        }
    }

    $Json.roots.bookmark_bar.children = @($keptChildren) + @($NewChildren)
    $Json.checksum = ""

    $outputJson = $Json | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($BookmarkFile, $outputJson, [System.Text.Encoding]::UTF8)

    # Enforce Bookmark Bar visibility
    Write-Host "Enforcing Bookmark Bar visibility in $profileDir..." -ForegroundColor Cyan
    $PrefsJson = if (Test-Path $PreferencesFile) {
        try { Get-Content $PreferencesFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch { [PSCustomObject]@{} }
    } else {
        [PSCustomObject]@{}
    }
    if (-not $PrefsJson) { $PrefsJson = [PSCustomObject]@{} }
    if (-not ($PrefsJson.PSObject.Properties['bookmark_bar'])) {
        $PrefsJson | Add-Member -NotePropertyName 'bookmark_bar' -NotePropertyValue ([PSCustomObject]@{})
    }
    if (-not ($PrefsJson.bookmark_bar.PSObject.Properties['show_on_all_tabs'])) {
        $PrefsJson.bookmark_bar | Add-Member -NotePropertyName 'show_on_all_tabs' -NotePropertyValue $true
    } else {
        $PrefsJson.bookmark_bar.show_on_all_tabs = $true
    }
    $outputPrefs = $PrefsJson | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($PreferencesFile, $outputPrefs, [System.Text.Encoding]::UTF8)
}

Write-Host "Done! Launching Chrome..." -ForegroundColor Green
$chromeExe = "chrome.exe"
$commonChrome = "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe"
$commonChromeX86 = "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
if (Test-Path $commonChrome) { $chromeExe = $commonChrome }
elseif (Test-Path $commonChromeX86) { $chromeExe = $commonChromeX86 }
Start-Process $chromeExe -ErrorAction SilentlyContinue
""";
        }

        private async Task InstallBartenderDrivers(IEnumerable<string> selectedApps)
        {
            string? selectedDriver = null;
            if (selectedApps.Contains("Argox Driver")) selectedDriver = "Argox Driver";
            else if (selectedApps.Contains("Zebra Driver")) selectedDriver = "Zebra Driver";

            if (string.IsNullOrEmpty(selectedDriver))
            {
                Log("   [SKIP] No Bartender driver selected.");
                return;
            }

            Log($"   [INIT] Installing {selectedDriver}...");

            string? driverExe = selectedDriver switch
            {
                "Argox Driver" => ResolveAssetPath("argox_drvr.exe"),
                "Zebra Driver" => ResolveAssetPath("zebra_drvr.exe"),
                _ => throw new InvalidOperationException("Invalid driver selection")
            };

            if (!string.IsNullOrEmpty(driverExe) && File.Exists(driverExe))
            {
                await RunProcessAsync(driverExe, "", $"Installing {selectedDriver} (Interactive)");
            }
            else
            {
                Log($"   [ERROR] {selectedDriver} installer not found in Assets.");
            }
            IncrementProgress();
        }
        private async Task InstallBartender(IEnumerable<string> selectedApps)
        {
            string? selectedVersion = null;
            string installerExe = null!;

            if (selectedApps.Contains("Bartender 10.1"))
            {
                selectedVersion = "Bartender 10.1";

                installerExe = "bt10.1.exe";
            }
            else if (selectedApps.Contains("Bartender 2016"))
            {
                selectedVersion = "Bartender 2016";
                installerExe = "bt2016.exe";
            }
            else if (selectedApps.Contains("Bartender 2022"))
            {
                selectedVersion = "Bartender 2022";
                installerExe = "bt2022.exe";
            }

            if (string.IsNullOrEmpty(selectedVersion))
            {
                Log("   [SKIP] No Bartender version selected.");
                return;
            }

            Log($"   [INIT] Starting {selectedVersion} Installation...");

            string? installerPath = ResolveAssetPath(installerExe);

            if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
            {
                Log($"   [ERROR] {installerExe} not found in Assets.");
                return;
            }

            await RunProcessAsync(installerPath, "/S", $"Installing {selectedVersion}");
            if (selectedVersion == "Bartender 10.1")
            {
                await Task.Delay(3000);
                Log("   [PATCH] Applying BarTend.exe patch for Bartender 10.1...");


                string? patchedExe = ResolveAssetPath("bartend.exe");
                string targetExe = @"C:\Program Files (x86)\Seagull\BarTender Suite\BarTend.exe";

                if (!string.IsNullOrEmpty(patchedExe) && File.Exists(patchedExe))
                {
                    try
                    {
                        var processes = Process.GetProcessesByName("BarTend");
                        foreach (var proc in processes)
                        {
                            proc.Kill();
                            proc.WaitForExit();
                        }

                        if (File.Exists(targetExe))
                        {
                            File.Copy(targetExe, targetExe + ".backup", true);
                            Log("   [BACKUP] Original BarTend.exe backed up.");
                        }

                        File.Copy(patchedExe, targetExe, true);
                        Log("   [SUCCESS] BarTend.exe replaced successfully.");
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Failed to replace BarTend.exe: {ex.Message}");
                    }
                }
                else
                {
                    Log("   [WARN] Patched bartend.exe not found in Assets folder.");
                }
            }

            string? btZip = ResolveAssetPath("bt.zip");
            string templatesDest = @"C:\Bartender Templates";

            if (!string.IsNullOrEmpty(btZip) && File.Exists(btZip))
            {
                Log("   [DEPLOY] Setting up Bartender Templates...");
                try
                {
                    if (Directory.Exists(templatesDest))
                        Directory.Delete(templatesDest, true);

                    Directory.CreateDirectory(templatesDest);
                    await ExtractWithProgress(btZip, templatesDest, CreateStepProgress("Extracting BarTender Templates"));
                    Log($"   [SUCCESS] Templates extracted to {templatesDest}");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to deploy templates: {ex.Message}");
                }
            }
            else
            {
                Log("   [WARN] bt.zip (Templates) not found in Assets.");
            }
            IncrementProgress();
        }
        private void EnsureWpsShortcutsForAllUsers()
        {
            try
            {
                string wpsInstallDir = @"C:\Program Files\WPS Office";
                if (!Directory.Exists(wpsInstallDir)) return;

                string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string publicStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "WPS Office");

                if (!Directory.Exists(publicStartMenu)) Directory.CreateDirectory(publicStartMenu);

                string[] wpsExeNames = ["wps.exe", "wpp.exe", "et.exe"];
                var exePaths = Directory.GetFiles(wpsInstallDir, "*.exe", SearchOption.AllDirectories)
                    .Where(f => wpsExeNames.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var exePath in exePaths)
                {
                    string name = Path.GetFileNameWithoutExtension(exePath);
                    string friendlyName = name switch
                    {
                        "wps" => "WPS Writer",
                        "wpp" => "WPS Presentation",
                        "et" => "WPS Spreadsheets",
                        _ => name
                    };

                    string desktopLnk = Path.Combine(publicDesktop, $"{friendlyName}.lnk");
                    string startLnk = Path.Combine(publicStartMenu, $"{friendlyName}.lnk");

                    if (!File.Exists(desktopLnk) || !File.Exists(startLnk))
                    {
                        string script = $"$ws = New-Object -ComObject WScript.Shell; " +
                                        $"$s = $ws.CreateShortcut('{desktopLnk}'); $s.TargetPath = '{exePath}'; $s.WorkingDirectory = '{wpsInstallDir}'; $s.Save(); " +
                                        $"$s2 = $ws.CreateShortcut('{startLnk}'); $s2.TargetPath = '{exePath}'; $s2.WorkingDirectory = '{wpsInstallDir}'; $s2.Save();";

                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var p = Process.Start(psi);
                        p?.WaitForExit(5000);
                    }
                }
                Log("   [SUCCESS] WPS Office shortcuts ensured for All Users.");
            }
            catch (Exception ex)
            {
                Log($"   [WARN] Failed to ensure WPS shortcuts for all users: {ex.Message}");
            }
        }

        private void CreateShortcutForWps(string shortcutPath, string targetPath, string workingDir)
        {
            string script = $"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{shortcutPath}'); $s.TargetPath = '{targetPath}'; $s.WorkingDirectory = '{workingDir}'; $s.Save()";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }

        private async Task InstallWampVersion(IEnumerable<string> selectedApps)
        {
            string? selectedVersion = null;
            if (selectedApps.Contains("Wamp 1.7.2")) selectedVersion = "Wamp 1.7.2";
            else if (selectedApps.Contains("Wamp 2")) selectedVersion = "Wamp 2";
            else if (selectedApps.Contains("Wamp 2.5")) selectedVersion = "Wamp 2.5";
            else if (selectedApps.Contains("Wampserver 3.4.0")) selectedVersion = "Wampserver 3.4.0";

            if (string.IsNullOrEmpty(selectedVersion))
            {
                Log("   [SKIP] No Wamp version selected.");
                return;
            }

            Log($"   [INIT] Installing {selectedVersion}...");

            switch (selectedVersion)
            {
                case "Wamp 1.7.2":
                    await SmartInstall("Wamp5 1.7.2", "wamp5.exe", "/S", "WampServer");
                    break;
                case "Wamp 2":
                    await SmartInstall("WampServer 2", "wamp2.exe", "/S", "WampServer");
                    break;
                case "Wamp 2.5":
                    await SmartInstall("WampServer 2.5", "wamp2.5.exe", "/S", "WampServer");
                    break;
                case "Wampserver 3.4.0":
                    await InstallWampServer();
                    break;
            }
        }
    }
}