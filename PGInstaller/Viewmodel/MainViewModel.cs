using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression; 
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    public partial class MainViewModel : ObservableObject
    {
        // Data Properties
        [ObservableProperty]
        private string? _logOutput;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _selectedDepartment;

        public ObservableCollection<string> Departments { get; } = new ObservableCollection<string>
        {
            "IT", "HRD", "ICD", "Payables", "Creative", "Admin", "Audit",
            "Store Operations (Manager)", "Store Operations (Customer Service)", "Store Operations (Gcash)", "Store Operations (HBC)", "Receiving", "Treasury"
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
                await EnsureWinget();

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
                    case "Store Operations (Gcash)":
                        await InstallStoreOperationsPackage("Gcash");
                        break;
                    case "Store Operations (HBC)":
                        await InstallStoreOperationsPackage("HBC");
                        break;
                    case "Creative":
                        await InstallCreativePackage();
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

        private async Task PrepareAssets()
        {
            string assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            string zipFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets.zip");

            if (!Directory.Exists(assetsDir))
            {
                if (File.Exists(zipFile))
                {
                    Log("   [INFO] Assets folder missing. Extracting Assets.zip...");
                    try
                    {
                       
                        await Task.Run(() => ZipFile.ExtractToDirectory(zipFile, AppDomain.CurrentDomain.BaseDirectory));
                        Log("   [SUCCESS] Extraction complete.");
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Failed to extract assets: {ex.Message}");
                    }
                }
                else
                {
                    Log("   [WARNING] 'Assets' folder AND 'Assets.zip' are missing. Local file installations will fail.");
                }
            }
            else
            {
                Log("   [INFO] Assets folder detected. Proceeding...");
            }
        }

        #region All Departments Common Installation
        private async Task InstallCommonPackages()
        {
            await RunWingetInstall("Google.Chrome", "Google Chrome");
            await RunWingetInstall("Mozilla.Firefox", "Mozilla Firefox");
            await RunWingetInstall("7zip.7zip", "7-Zip");
            await RunWingetInstall("Notepad++.Notepad++", "Notepad++");
            await RunWingetInstall("Thunderbird.Thunderbird", "Mozilla Thunderbird");


        }
        #endregion

        #region IT Department Installation
        private async Task InstallITPackage()
        {
            await InstallCommonPackages();
            await RunWingetInstall("Zoom.Zoom", "Zoom");

            if (await RunWingetInstall("PuTTY.PuTTY", "PuTTY"))
            {
                string regFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "putty_settings.reg");
                if (File.Exists(regFile))
                {
                    await RunProcessAsync("reg", $"import \"{regFile}\"", "Applying PuTTY Settings");
                }
                else
                {
                    Log($"   [INFO] Custom PuTTY settings file not found at: {regFile}");
                }
            }

            string radminInstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Radmin_Server.exe");
            if (File.Exists(radminInstaller))
            {
                await RunProcessAsync(radminInstaller, "/silence", "Installing Radmin Server");

                string sourcePbr = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "radmin.pbr");
                string destPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Radmin", "radmin.pbr"); // Usually ProgramData

                if (File.Exists(sourcePbr))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        File.Copy(sourcePbr, destPath, true);
                        Log("   [SUCCESS] Radmin configuration applied.");
                    }
                    catch (Exception ex) { Log($"   [ERROR] Failed to copy Radmin config: {ex.Message}"); }
                }
            }
            else
            {
                Log($"   [INFO] Radmin installer not found in Assets (Skipped).");
            }
        }
        #endregion

        #region HRD Department Installation
        private async Task InstallHRDPackage()
        {
            await RunProcessAsync("dism", "/online /enable-feature /featurename:NetFX3 /all /NoRestart", "Enabling .NET Framework 3.5");
            await InstallCommonPackages();
        }

        #endregion

        #region Admin Department Installation
        private async Task InstallAdminPackage()
        {
           Log("   [INFO] No specific installations defined for Admin Department yet.");
            await InstallCommonPackages();
        }
        #endregion

        #region Audit Department Installation

        private async Task InstallAuditPackage()
        {
           Log("   [INFO] No specific installations defined for Audit Department yet.");
            await InstallCommonPackages();
        }

        #endregion

        #region Payables Department Installation
        private async Task InstallPayablesPackage()
        {
           Log("   [INFO] No specific installations defined for Payables Department yet.");
            await InstallCommonPackages();
        }

        #endregion

        #region Creative Department Installation
        private async Task InstallCreativePackage()
        {
           Log("   [INFO] No specific installations defined for Creative Department yet.");
            await InstallCommonPackages();
        }
        #endregion

        #region Store Operations Department Installation
        private async Task InstallStoreOperationsPackage(string role)
        {
           Log($"   [INFO] No specific installations defined for Store Operations - {role} yet.");
           await InstallCommonPackages();

            if (role == "Manager")
            {
                await RunWingetInstall("Microsoft.Teams", "Microsoft Teams");
            }
            if (role == "Customer Service")
            {
                await RunWingetInstall("Zoom.Zoom", "Zoom");
            }
            if (role == "Gcash")
            {
                await RunWingetInstall("GitHub.cli", "GitHub CLI");
            }
            if (role == "HBC")
            {
                await RunWingetInstall("SlackTechnologies.Slack", "Slack");
            }

        }
        #endregion

        #region Receiving Department Installation
        private async Task InstallReceivingPackage()
        {
           Log("   [INFO] No specific installations defined for Receiving Department yet.");
            await InstallCommonPackages();
        }
        #endregion

        #region Treasury Department Installation
        private async Task InstallTreasuryPackage()
        {
           Log("   [INFO] No specific installations defined for Treasury Department yet.");
            await InstallCommonPackages();
        }
        #endregion

        #region ICD Department Installation
        private async Task InstallICDPackage()
        {
           Log("   [INFO] No specific installations defined for ICD Department yet.");
            await InstallCommonPackages();
        }
        #endregion

        #region Winget Helpers
        private async Task EnsureWinget()
        {
            await RunProcessAsync("winget", "-v", "Checking Winget");
        }

        private async Task<bool> RunWingetInstall(string id, string appName)
        {
            string args = $"install --id {id} -e --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity";
            return await RunProcessAsync("winget", args, $"Installing {appName}");
        }

        private async Task<bool> RunProcessAsync(string fileName, string arguments, string description)
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
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string? cleanLine = CleanLogLine(e.Data);
                    if (!string.IsNullOrEmpty(cleanLine))
                    {
                        Log($"   > {cleanLine}");
                    }
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string? cleanLine = CleanLogLine(e.Data);
                    if (!string.IsNullOrEmpty(cleanLine))
                    {
                        Log($"   > {cleanLine}");
                    }
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
                Log($"   [FAILED] Could not start {fileName}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Log Cleaning
        private string? CleanLogLine(string line)
        {
            line = line.Trim();
            if (line.Contains("█") || line.Contains("▒") || line.StartsWith("[=") || line.StartsWith("=======")) return null;
            if (line.Length <= 2 && (line.Contains("-") || line.Contains("\\") || line.Contains("|") || line.Contains("/"))) return null;
            if (Regex.IsMatch(line, @"\d+\s?(KB|MB|GB)\s?/\s?\d+\s?(KB|MB|GB)")) return null;
            if (Regex.IsMatch(line, @"\d+%$")) return null;
            if (line.StartsWith("Download") && !line.Contains("http")) return null;

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