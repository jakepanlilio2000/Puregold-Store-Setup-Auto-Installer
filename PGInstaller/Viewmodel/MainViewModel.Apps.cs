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
                await InstallZipPackage("MMS.zip", "setup.exe", "", "MMS (IBM Personal Communications)");

                string mmsSource = Path.Combine(_assetsPath, "MMS.ws");
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string mmsDest = Path.Combine(desktopPath, "MMS.ws");

                if (File.Exists(mmsSource))
                {
                    if (!File.Exists(mmsDest))
                    {
                        try
                        {
                            File.Copy(mmsSource, mmsDest);
                            Log("   [SUCCESS] Copied MMS.ws to Desktop.");
                        }
                        catch (Exception ex)
                        {
                            Log($"   [ERROR] Failed to copy MMS.ws: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log("   [SKIP] MMS.ws already exists on Desktop.");
                    }
                }
                else
                {
                    Log("   [WARNING] MMS.ws not found in Assets.");
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
                if (File.Exists(Path.Combine(_assetsPath, "newtstop.dll")) &&
                    File.Exists(Path.Combine(_assetsPath, "nts64helper.dll")))
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
                    Log("   [ERROR] Dependencies for install.bat (newtstop.dll or nts64helper.dll) missing.");
                }
            }
            else
            {
                Log("   [WARNING] install.bat not found in Assets.");
            }
        }

        private async Task PasteVARIANCE()
        {
            //variance function
        }

        private async Task InstallPIMS()
        {
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
                catch (Exception ex) { Log($"   [ERROR] Extraction failed: {ex.Message}"); return; }
            }

            string crDir = Path.Combine(pimsRoot, "CRDev8.5");
            string crSetup = Path.Combine(crDir, "setup.exe");

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
                await RunProcessAsync(crSetup, "", "Installing Crystal Reports 8.5");
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
            }
            else
            {
                Log($"   [ERROR] Source FMS folder not found at {fmsSource}");
            }
            Log("   [CONFIG] Requesting IP Address...");

            string ipAddress = await Application.Current.Dispatcher.InvokeAsync(() => ShowInputDialog("Enter Server IP Address:", "192.92.1.100"));

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
            if (File.Exists(path)) return true;
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
                    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = Path.GetRelativePath(sourceDir, file);
                        string destPath = Path.Combine(targetDir, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        File.Copy(file, destPath, true);
                    }
                });
                Log("   [SUCCESS] FMS Copied successfully.");
            }
            catch (Exception ex) { Log($"   [ERROR] Copy failed: {ex.Message}"); }
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
            System.Windows.Window window = new System.Windows.Window()
            {
                Title = "Configuration",
                Width = 350,
                Height = 180,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStyle = System.Windows.WindowStyle.ToolWindow
            };

            System.Windows.Controls.StackPanel stack = new System.Windows.Controls.StackPanel() { Margin = new System.Windows.Thickness(20) };

            stack.Children.Add(new System.Windows.Controls.TextBlock() { Text = question, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(0, 0, 0, 10) });

            System.Windows.Controls.TextBox txtAnswer = new System.Windows.Controls.TextBox() { Text = defaultAnswer, Height = 30, VerticalContentAlignment = System.Windows.VerticalAlignment.Center };
            stack.Children.Add(txtAnswer);

            System.Windows.Controls.Button btnOk = new System.Windows.Controls.Button() { Content = "OK", IsDefault = true, Height = 30, Width = 80, Margin = new System.Windows.Thickness(0, 20, 0, 0), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            stack.Children.Add(btnOk);

            string result = "";

            btnOk.Click += (s, e) => { result = txtAnswer.Text; window.DialogResult = true; window.Close(); };

            window.Content = stack;
            window.ShowDialog();

            return result;
        }

        private async Task InstallFSDM()
        {

        }

        private async Task InstallCorelPSIllu()
        {
            string corelExe = "crdx5.exe";
            string corelPath = Path.Combine(_assetsPath, corelExe);

            if (File.Exists(corelPath))
            {
                await RunProcessAsync(corelPath, "", "Launching CorelDRAW X5 Installer");
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
                    catch (Exception ex) { Log($"   [ERROR] Illustrator extraction failed: {ex.Message}"); }
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
                    catch (Exception ex) { Log($"   [ERROR] Photoshop extraction failed: {ex.Message}"); }
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
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktopPath, $"{linkName}.lnk");

            if (!File.Exists(shortcutPath))
            {
                Log($"   [SHORTCUT] Creating {linkName} on Desktop...");
                string script = $"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{shortcutPath}'); $s.TargetPath = '{targetPath}'; $s.Save()";

                await RunProcessAsync("powershell", $"-Command \"{script}\"", $"Creating Shortcut: {linkName}", true);
            }
        }

        private async Task InstallPutty()
        {
            await SmartInstall("PuTTY", "putty.msi", "/qn", "PuTTY");
            string regFile = Path.Combine(_assetsPath, "Zone 11.reg");

            if (File.Exists(regFile))
            {
                await RunProcessAsync("reg", $"import \"{regFile}\"", "Applying Zone 11 Registry Settings");
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
                    string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
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
    }
}
