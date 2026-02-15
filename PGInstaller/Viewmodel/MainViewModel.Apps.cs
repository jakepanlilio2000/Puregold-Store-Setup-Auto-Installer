using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallMMS()
        {
            if (!IsAppInstalled("IBM Personal Communications"))
            {
                await InstallZipPackage(
                    "MMS.zip",
                    "setup.exe",
                    "",
                    "MMS (IBM Personal Communications)"
                );

                string mmsFileName = "MMS.ws";
                string mmsSource = Path.Combine(_assetsPath, mmsFileName);
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string mmsDest = Path.Combine(desktopPath, mmsFileName);

                if (File.Exists(mmsSource))
                {
                    if (!File.Exists(mmsDest))
                    {
                        try
                        {
                            File.Copy(mmsSource, mmsDest);
                            Log($"   [COPY] Copied {mmsFileName} to Desktop.");
                        }
                        catch (Exception ex)
                        {
                            Log($"   [ERROR] Failed to copy {mmsFileName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log($"   [SKIP] {mmsFileName} already exists on Desktop.");
                    }
                }
                else
                {
                    Log($"   [WARN] {mmsFileName} not found in Assets root.");
                }
                string kmpFileName = "AS400.KMP";
                string kmpSource = Path.Combine(_assetsPath, kmpFileName);
                string kmpDest = @"C:\AS400.KMP";

                if (File.Exists(kmpSource))
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
                else
                {
                    Log($"   [WARN] {kmpFileName} not found in Assets.");
                }
            }
            else
            {
                Log("   [SKIP] MMS (IBM Personal Communications) is already installed.");
            }
        }
        private async Task ApplyRadminServer()
        {
            string installBatPath = Path.Combine(_assetsPath, "install.bat");
            if (File.Exists(installBatPath))
            {
                if (
                    File.Exists(Path.Combine(_assetsPath, "newtstop.dll"))
                    && File.Exists(Path.Combine(_assetsPath, "nts64helper.dll"))
                )
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{installBatPath}\"",
                        WorkingDirectory = _assetsPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    await RunCustomProcess(startInfo, "Applying Radmin NewTrialStop Patch");
                }
                else
                {
                    Log(
                        "   [ERROR] Dependencies for install.bat (newtstop.dll or nts64helper.dll) missing."
                    );
                }
            }
            else
            {
                Log("   [WARNING] install.bat not found in Assets.");
            }
        }

        private async Task InstallInventoryTools()
        {
            string zipName = "inventorytools.zip";
            string zipPath = Path.Combine(_assetsPath, zipName);
            string targetDir = @"C:\wamp64\www\puregold";

            if (File.Exists(zipPath))
            {
                Log($"   [DEPLOY] Deploying {zipName}...");

                try
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    await Task.Run(() =>
                    {
                        ZipFile.ExtractToDirectory(zipPath, targetDir, true);
                    });

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
        }

        private async Task PinToTaskbar(string appName, string exeName)
        {
            string[] searchPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                @"C:\Program Files\Google\Chrome\Application",
                @"C:\Program Files\Mozilla Firefox",
                @"C:\Program Files\Mozilla Thunderbird"
            };

            string? shortcutPath = null;

            foreach (var dir in searchPaths)
            {
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly));

                    var match = files.FirstOrDefault(f => Path.GetFileName(f).Equals(exeName, StringComparison.OrdinalIgnoreCase) ||
                                                          Path.GetFileNameWithoutExtension(f).Equals(appName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        shortcutPath = match;
                        break;
                    }
                }
            }

            if (shortcutPath != null)
            {
                try
                {
                    string script = $@"
                    $path = '{shortcutPath}'
                    $shell = New-Object -ComObject Shell.Application
                    $folder = $shell.NameSpace((Get-Item $path).DirectoryName)
                    $item = $folder.ParseName((Get-Item $path).Name)
                    $verb = $item.Verbs() | Where-Object {{ $_.Name -like '*taskbar*' }}
                    if ($verb) {{ $verb.DoIt() }}
                    ";

                    await RunProcessAsync("powershell.exe", $"-Command \"{script}\"", $"Pinning {appName}", true);
                }
                catch {  }
            }
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
                        ? Directory.GetDirectories(apacheRoot).FirstOrDefault(d => Path.GetFileName(d).StartsWith("apache"))
                        : null;

                    if (apacheVerDir != null)
                    {
                        string vhostPath = Path.Combine(apacheVerDir, @"conf\extra\httpd-vhosts.conf");
                        if (File.Exists(vhostPath))
                        {
                            string newVhostConfig = @"
# Virtual Hosts
#
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
                        ? Directory.GetDirectories(phpRoot).FirstOrDefault(d => Path.GetFileName(d).StartsWith("php8.3"))
                        : null;

                    if (phpVerDir != null)
                    {
                        string extDir = Path.Combine(phpVerDir, "ext");
                        string dll1 = "php_sqlsrv_83_ts_x64.dll";
                        string dll2 = "php_pdo_sqlsrv_83_ts_x64.dll";

                        string sourceDll1 = Path.Combine(_assetsPath, dll1);
                        string sourceDll2 = Path.Combine(_assetsPath, dll2);

                        if (File.Exists(sourceDll1)) File.Copy(sourceDll1, Path.Combine(extDir, dll1), true);
                        if (File.Exists(sourceDll2)) File.Copy(sourceDll2, Path.Combine(extDir, dll2), true);
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
                            Verb = "runas"
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Wamp Config Failed: {ex.Message}");
                }
            }
        }

        private async Task PasteVARIANCE()
        {
            string varianceZip = Path.Combine(_assetsPath, "variance.zip");
            string targetDir = @"C:\wamp64\www\puregold";

            if (File.Exists(varianceZip))
            {
                Log("   [DEPLOY] Unzipping Variance System...");
                try
                {
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                    await Task.Run(() => ZipFile.ExtractToDirectory(varianceZip, targetDir, true));
                    Log("   [SUCCESS] Variance deployed to www/puregold.");
                }
                catch (Exception ex) { Log($"   [ERROR] Variance Deploy failed: {ex.Message}"); }
            }
            else
            {
                Log("   [WARN] variance.zip not found in Assets.");
            }
        }

        private string GetNetFxSourceFolder()
        {
            try
            {
                var osVersion = Environment.OSVersion.Version;
                string osLabel = (osVersion.Build >= 22000) ? "Win11" : "Win10";

                string releaseId = "";
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        releaseId = key.GetValue("DisplayVersion")?.ToString() ??
                                    key.GetValue("ReleaseId")?.ToString() ??
                                    "";
                    }
                }

                if (string.IsNullOrEmpty(releaseId)) return null!;
                return $"{osLabel} {releaseId.ToLower()}";
            }
            catch (Exception ex)
            {
                Log($"   [WARN] OS Detection error: {ex.Message}");
                return null!;
            }
        }
        private async Task InstallNetFx3()
        {
            Log("   [INIT] Starting Offline .NET 3.5 Installation...");
            string netfxZip = Path.Combine(_assetsPath, "netfx.zip");
            string netfxExtractDir = @"C:\Assets\NetFX3_Source";

            if (!File.Exists(netfxZip))
            {
                Log("   [ERROR] netfx.zip not found in Assets.");
                return;
            }

            if (!Directory.Exists(netfxExtractDir))
            {
                Log("   [EXTRACT] Unzipping NetFX3 sources...");
                try
                {
                    Directory.CreateDirectory(netfxExtractDir);
                    await Task.Run(() => ZipFile.ExtractToDirectory(netfxZip, netfxExtractDir));
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Extraction failed: {ex.Message}");
                    return;
                }
            }
            string matchedFolder = GetNetFxSourceFolder();

            if (string.IsNullOrEmpty(matchedFolder))
            {
                Log("   [ERROR] Could not detect a compatible source folder for this OS.");
                return;
            }

            string sourcePath = Path.Combine(netfxExtractDir, matchedFolder);
            if (!Directory.Exists(sourcePath))
            {
                string osPrefix = matchedFolder.Split(' ')[0];
                var fallbackDir = Directory.GetDirectories(netfxExtractDir, osPrefix + "*").FirstOrDefault();

                if (fallbackDir != null)
                {
                    Log($"   [WARN] Exact version '{matchedFolder}' not found. Using fallback: {Path.GetFileName(fallbackDir)}");
                    sourcePath = fallbackDir;
                }
                else
                {
                    Log($"   [ERROR] Source directory not found: {matchedFolder}");
                    return;
                }
            }

            Log($"   [INSTALL] Installing from source: {Path.GetFileName(sourcePath)}");
            bool success = await RunProcessAsync(
                "dism",
                $"/Online /Enable-Feature /FeatureName:NetFx3 /All /Source:\"{sourcePath}\" /LimitAccess /NoRestart",
                "Enabling .NET 3.5 (Offline)"
            );

            if (success)
                Log("   [SUCCESS] .NET Framework 3.5 installed.");
            else
                Log("   [ERROR] Installation failed. Check logs.");
        }
        private async Task InstallPIMS()
        {
            await InstallNetFx3();

            string pimsZip = Path.Combine(_assetsPath, "pims.zip");
            string pimsRoot = Path.Combine(Path.GetTempPath(), "PG_PIMS_Install");

            if (!File.Exists(pimsZip))
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
                    await Task.Run(() => ZipFile.ExtractToDirectory(pimsZip, pimsRoot));
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Extraction failed: {ex.Message}");
                    return;
                }
            }
            string crDir = Path.Combine(pimsRoot, "CR10");
            string crSetup = Path.Combine(crDir, "CR10_Autorun_ENPRO.exe");

            if (VerifyFile(crSetup))
            {
                string serialPath = Path.Combine(crDir, "Serial Number.txt");
                if (File.Exists(serialPath))
                {
                    Log("   [INFO] Launching Serial Number.txt...");
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = serialPath, UseShellExecute = true });
                    }
                    catch (Exception ex) { Log($"   [WARN] Could not open serial file: {ex.Message}"); }
                }
                await RunProcessAsync(crSetup, "", "Installing Crystal Reports 8.5/10");
            }
            string crRedist86 = Path.Combine(pimsRoot, "CRRedist2005_x86.msi");
            if (VerifyFile(crRedist86))
            {
                await RunProcessAsync("msiexec.exe", $"/i \"{crRedist86}\" /qn", "Installing CR Redist 2005 (x86)");
            }

            if (Environment.Is64BitOperatingSystem)
            {
                string crRedist64 = Path.Combine(pimsRoot, "CRRedist2005_X64.msi");
                if (VerifyFile(crRedist64))
                {
                    await RunProcessAsync("msiexec.exe", $"/i \"{crRedist64}\" /qn", "Installing CR Redist 2005 (x64)");
                }
            }
            string poMsi = Path.Combine(pimsRoot, "POTracking", "POTracking.msi");
            if (VerifyFile(poMsi))
            {
                await RunProcessAsync("msiexec.exe", $"/i \"{poMsi}\" /qn", "Installing POTracking");
            }
            string sqlDir = Path.Combine(pimsRoot, "SQLServer2005");
            string sqlMsi = Environment.Is64BitOperatingSystem
                ? Path.Combine(sqlDir, "SQLServer2005_BC_x64.msi")
                : Path.Combine(sqlDir, "SQLServer2005_BC.msi");

            if (VerifyFile(sqlMsi))
            {
                await RunProcessAsync("msiexec.exe", $"/i \"{sqlMsi}\"", "Installing SQL Server 2005 BC");
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
                    await CreateDesktopShortcut("PIMS", pimsExe);
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

            Log("   [CONFIG] Requesting IP Address...");

            string ipAddress = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter Server IP Address config for FIDB and FIHO:", "192.92.1.100")
            );

            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                try
                {
                    ConfigurePimsRegistry("FIHO", ipAddress);
                    ConfigurePimsRegistry("FIDB", ipAddress);
                    Log("   [SUCCESS] Registry Configuration Applied.");
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
                    foreach (
                        var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
                    )
                    {
                        string relativePath = Path.GetRelativePath(sourceDir, file);
                        string destPath = Path.Combine(targetDir, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
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

            using (var key = Registry.LocalMachine.CreateSubKey(baseKey))
            {
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
        }

        private string ShowInputDialog(string question, string defaultAnswer = "")
        {
            System.Windows.Window window = new()
            {
                Title = "Configuration",
                Width = 350,
                Height = 180,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStyle = System.Windows.WindowStyle.ToolWindow,
            };

            System.Windows.Controls.StackPanel stack = new System.Windows.Controls.StackPanel()
            {
                Margin = new System.Windows.Thickness(20),
            };

            stack.Children.Add(
                new System.Windows.Controls.TextBlock()
                {
                    Text = question,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Margin = new System.Windows.Thickness(0, 0, 0, 10),
                }
            );

            System.Windows.Controls.TextBox txtAnswer = new System.Windows.Controls.TextBox()
            {
                Text = defaultAnswer,
                Height = 30,
                VerticalContentAlignment = System.Windows.VerticalAlignment.Center,
            };
            stack.Children.Add(txtAnswer);

            System.Windows.Controls.Button btnOk = new System.Windows.Controls.Button()
            {
                Content = "OK",
                IsDefault = true,
                Height = 30,
                Width = 80,
                Margin = new System.Windows.Thickness(0, 20, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            };
            stack.Children.Add(btnOk);

            string result = "";

            btnOk.Click += (s, e) =>
            {
                result = txtAnswer.Text;
                window.DialogResult = true;
                window.Close();
            };

            window.Content = stack;
            window.ShowDialog();

            return result;
        }

        private async Task InstallFSDM()
        {
            string fsdmZip = Path.Combine(_assetsPath, "FSDM.zip");
            string tempFsdmRoot = Path.Combine(Path.GetTempPath(), "PG_FSDM_Install");

            if (Directory.Exists(tempFsdmRoot))
                try { Directory.Delete(tempFsdmRoot, true); } catch { }

            if (!File.Exists(fsdmZip))
            {
                Log("   [ERROR] FSDM.zip not found in Assets.");
                return;
            }

            Log("   [INIT] Extracting FSDM.zip...");
            try
            {
                Directory.CreateDirectory(tempFsdmRoot);
                await Task.Run(() => ZipFile.ExtractToDirectory(fsdmZip, tempFsdmRoot));
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
                    await Task.Run(() => ZipFile.ExtractToDirectory(fsDevZip, fsDestDir));
                }
                catch (Exception ex) { Log($"   [WARN] FSDevMan extract issue: {ex.Message}"); }
                string exePath = Path.Combine(fsDestDir, "FSDeviceManager.exe");

                if (File.Exists(exePath))
                {
                    await CreateDesktopShortcut("FSDM", exePath);
                    Log("   [SHORTCUT] Created Desktop Shortcut: FSDM");
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
                    await Task.Run(() => ZipFile.ExtractToDirectory(sdkZip, sdkTempDir));

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
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string dbDest = Path.Combine(desktop, dbUpdater);
                try
                {
                    File.Copy(dbSource, dbDest, true);
                    Log("   [COPY] FSDM Database Updater copied to Desktop.");
                }
                catch (Exception ex) { Log($"   [ERROR] Failed copy updater: {ex.Message}"); }
            }
        }
        private async Task InstallCorelPSIllu()
        {
            string corelExe = "crdx5.exe";
            string corelPath = Path.Combine(_assetsPath, corelExe);

            if (File.Exists(corelPath))
            {
                await RunProcessAsync(corelPath, "", "Launching CorelDRAW X5 Installer");

                string prog86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string installedPath = Path.Combine(prog86, @"Corel\CorelDRAW Graphics Suite X5\Programs\CorelDRW.exe");

                if (!File.Exists(installedPath))
                {
                    string prog64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    installedPath = Path.Combine(prog64, @"Corel\CorelDRAW Graphics Suite X5\Programs\CorelDRW.exe");
                }

                if (File.Exists(installedPath))
                {
                    await CreateDesktopShortcut("CorelDRAW X5", installedPath);
                }
                else
                {
                    Log("   [WARN] Could not locate CorelDRW.exe to create shortcut.");
                }
            }
            else
            {
                Log($"   [SKIP] Corel installer not found: {corelExe}");
            }

            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string illuZip = Path.Combine(_assetsPath, "illucs6.zip");
            if (File.Exists(illuZip))
            {
                string destDir = Path.Combine(progFiles, "IllustratorCS6Portable");
                string exePath = Path.Combine(destDir, "IllustratorCS6Portable.exe");

                if (!Directory.Exists(destDir))
                {
                    Log("   [EXTRACT] Unzipping Illustrator CS6 to Program Files...");
                    try
                    {
                        Directory.CreateDirectory(destDir);
                        await Task.Run(() => ZipFile.ExtractToDirectory(illuZip, destDir));
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Illustrator extraction failed: {ex.Message}");
                    }
                }
                if (File.Exists(exePath))
                {
                    await CreateDesktopShortcut("Illustrator CS6", exePath);
                }
            }
            else
            {
                Log("   [SKIP] illucs6.zip not found.");
            }

            string psZip = Path.Combine(_assetsPath, "pscs6.zip");
            if (File.Exists(psZip))
            {
                string destDir = Path.Combine(progFiles, "PhotoshopCS6Portable");
                string exePath = Path.Combine(destDir, "PhotoshopCS6Portable.exe");

                if (!Directory.Exists(destDir))
                {
                    Log("   [EXTRACT] Unzipping Photoshop CS6 to Program Files...");
                    try
                    {
                        Directory.CreateDirectory(destDir);
                        await Task.Run(() => ZipFile.ExtractToDirectory(psZip, destDir));
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Photoshop extraction failed: {ex.Message}");
                    }
                }

                if (File.Exists(exePath))
                {
                    await CreateDesktopShortcut("Photoshop CS6", exePath);
                }
            }
            else
            {
                Log("   [SKIP] pscs6.zip not found.");
            }
        }

        private async Task CreateDesktopShortcut(string linkName, string targetPath)
        {
            string desktopPath = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory
            );
            string shortcutPath = Path.Combine(desktopPath, $"{linkName}.lnk");

            if (!File.Exists(shortcutPath))
            {
                Log($"   [SHORTCUT] Creating {linkName} on Desktop...");
                string script =
                    $"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{shortcutPath}'); $s.TargetPath = '{targetPath}'; $s.Save()";

                await RunProcessAsync(
                    "powershell",
                    $"-Command \"{script}\"",
                    $"Creating Shortcut: {linkName}",
                    true
                );
            }
        }

        private async Task InstallPutty()
        {
            await SmartInstall("PuTTY", "putty.msi", "/qn", "PuTTY");
            string regFile = Path.Combine(_assetsPath, "Zone 11.reg");

            if (File.Exists(regFile))
            {
                await RunProcessAsync(
                    "reg",
                    $"import \"{regFile}\"",
                    "Applying Zone 11 Registry Settings"
                );
            }
            else
            {
                Log($"   [WARN] Registry file missing: {Path.GetFileName(regFile)}");
            }
        }

        private async Task InstallRadminViewer()
        {
            await SmartInstall("Radmin Viewer", "radminv.msi", "/qn /norestart", "Radmin Viewer");
            string rpbName = "radmin.rpb";
            string sourceRpb = Path.Combine(_assetsPath, rpbName);

            if (File.Exists(sourceRpb))
            {
                try
                {
                    string roamingPath = Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData
                    );
                    string destDir = Path.Combine(roamingPath, "Radmin");
                    string destFile = Path.Combine(destDir, rpbName);

                    Directory.CreateDirectory(destDir);
                    File.Copy(sourceRpb, destFile, true);

                    Log($"   [CONFIG] Applied Radmin Phonebook to: {destDir}");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to copy Radmin phonebook: {ex.Message}");
                }
            }
            else
            {
                Log($"   [WARN] Radmin phonebook not found: {rpbName}");
            }
        }

        private async Task InstallWinSCP()
        {
            await SmartInstall(
                "WinSCP",
                "WinSCP.exe",
                "/VERYSILENT /NORESTART /ALLUSERS",
                "WinSCP"
            );

            string iniName = "WinSCP.ini";
            string iniSource = Path.Combine(_assetsPath, iniName);

            if (File.Exists(iniSource))
            {
                string[] installDirs =
                {
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        "WinSCP"
                    ),
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "WinSCP"
                    ),
                };

                bool copied = false;

                foreach (string dir in installDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        try
                        {
                            string destFile = Path.Combine(dir, iniName);
                            File.Copy(iniSource, destFile, true);
                            Log($"   [CONFIG] WinSCP.ini imported to: {dir}");
                            copied = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            Log($"   [ERROR] Failed to copy WinSCP.ini: {ex.Message}");
                        }
                    }
                }

                if (!copied)
                {
                    Log("   [WARN] WinSCP installation folder not found. INI not imported.");
                }
            }
            else
            {
                Log("   [SKIP] WinSCP.ini not found in Assets.");
            }
        }

        private async Task RunChromeBookmarkScript()
        {
            Log("------------------------------------------------");
            Log("   [INIT] Configuring Chrome Bookmarks (CBM)...");

            string ownIp = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter OWN IP (for Local Conso):", "192.168.1.xxx"));

            if (string.IsNullOrWhiteSpace(ownIp)) { Log("   [SKIP] IP missing."); return; }

            string scriptName = "cbm.ps1";
            string csvName = "port# & IP ZONE11.csv";
            string tempDir = Path.Combine(Path.GetTempPath(), "PG_CBM_Exec");

            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                string sourceCsv = Path.Combine(_assetsPath, csvName);
                if (!File.Exists(sourceCsv))
                {
                    var files = Directory.GetFiles(_assetsPath, "*.csv", SearchOption.AllDirectories);
                    var match = files.FirstOrDefault(f => Path.GetFileName(f).Equals(csvName, StringComparison.OrdinalIgnoreCase));
                    if (match != null) sourceCsv = match;
                }

                if (File.Exists(sourceCsv))
                {
                    File.Copy(sourceCsv, Path.Combine(tempDir, csvName), true);
                }
                else
                {
                    Log($"   [ERROR] CSV '{csvName}' not found. Script will fail.");
                    return;
                }

                string sourceScript = Path.Combine(_assetsPath, scriptName);
                if (!File.Exists(sourceScript))
                {
                    Log($"   [ERROR] {scriptName} not found in Assets.");
                    return;
                }

                string scriptContent = File.ReadAllText(sourceScript);
                scriptContent = scriptContent.Replace("{{OWN_IP}}", ownIp);

                string modifiedScriptPath = Path.Combine(tempDir, "cbm_modified.ps1");
                File.WriteAllText(modifiedScriptPath, scriptContent);
                Log("   [EXEC] Running CBM Script...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{modifiedScriptPath}\"",
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
        }

        private async Task InstallBartender()
        {
            Log("   [INIT] Starting Bartender 11.8 Installation...");

            string zipName = "bartender.zip";
            string zipPath = Path.Combine(_assetsPath, zipName);
            string extractDir = Path.Combine(GlobalTempRoot, "Bartender_Install");

            if (!File.Exists(zipPath))
            {
                Log($"   [ERROR] {zipName} not found in Assets.");
                return;
            }

            if (!Directory.Exists(extractDir))
            {
                Log("   [EXTRACT] Unzipping bartender.zip...");
                try
                {
                    Directory.CreateDirectory(extractDir);
                    await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractDir));
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Extraction failed: {ex.Message}");
                    return;
                }
            }
            string driverExe = Path.Combine(extractDir, "bartenderDR.exe");
            if (File.Exists(driverExe))
            {
                await RunProcessAsync(driverExe, "/S", "Installing Bartender Driver");
            }
            else
            {
                Log("   [WARN] bartenderDR.exe not found.");
            }
            string uiExe = Path.Combine(extractDir, "bartenderUI.exe");
            if (File.Exists(uiExe))
            {
                await RunProcessAsync(uiExe, "/S", "Installing Bartender UI");
            }
            else
            {
                Log("   [ERROR] bartenderUI.exe not found. Cannot proceed.");
                return;
            }
            Log("   [PATCH] Applying Crack...");

            string patchSrc = Path.Combine(extractDir, "patch", "BarTend.exe");

            if (!File.Exists(patchSrc))
            {
                var allFiles = Directory.GetFiles(extractDir, "BarTend.exe", SearchOption.AllDirectories);
                patchSrc = allFiles.FirstOrDefault(f => f.IndexOf("patch", StringComparison.OrdinalIgnoreCase) >= 0) ??
                           allFiles.FirstOrDefault()!;
            }

            if (File.Exists(patchSrc))
            {
                await Task.Run(() =>
                {
                    try { foreach (var p in Process.GetProcessesByName("BarTend")) p.Kill(); } catch { }
                    try { foreach (var p in Process.GetProcessesByName("BarTender")) p.Kill(); } catch { }
                });
                await Task.Delay(2000); 
                string prog64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string prog86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                string[] potentialDestinations = {
                    Path.Combine(prog64, @"Seagull\BarTender 11.8\BarTend.exe"),
                    Path.Combine(prog86, @"Seagull\BarTender 11.8\BarTend.exe"),
                    Path.Combine(prog64, @"Seagull\BarTender Suite\BarTender\BarTend.exe"),
                    Path.Combine(prog86, @"Seagull\BarTender Suite\BarTender\BarTend.exe")
                };
                string destPath = potentialDestinations.FirstOrDefault(File.Exists)!;

                if (destPath != null)
                {
                    try
                    {
                        File.Copy(patchSrc, destPath, true); 
                        Log($"   [SUCCESS] Patched: {destPath}");
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Failed to overwrite file: {ex.Message}");
                    }
                }
                else
                {
                    Log("   [WARN] Installation directory (Seagull/BarTender 11.8) not found.");
                    Log("           Please manually copy the patch file.");
                }
            }
            else
            {
                Log("   [ERROR] Patch file (BarTend.exe) not found in extracted zip.");
            }
        }
    }
}
