using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private bool CanRunTool() => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanRunTool))]
        private async Task RunActivator()
        {
            if (IsBusy) return;
            IsBusy = true;
            NotifyCommands();

            Log("------------------------------------------------");
            Log("Preparing Windows Activator...");

            try
            {
                // Lazy extraction: extract only the activator files rather than the entire assets.zip
                await ExtractSpecificFile(null, "*MAS_AIO*");

                string basePath = _assetsPath ?? @"C:\Assets";
                string primaryPath = Path.Combine(basePath, "activators", "MAS_AIO.cmd");
                string? sourcePath = File.Exists(primaryPath)
                    ? primaryPath
                    : (ResolveAssetPath(Path.Combine("activators", "MAS_AIO.cmd")) ?? ResolveAssetPath("MAS_AIO.cmd"));

                if (!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath))
                {
                    string destDir = @"C:\PG_Activator";
                    string scriptName = Path.GetFileName(sourcePath);
                    string destPath = Path.Combine(destDir, scriptName);

                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                    File.Copy(sourcePath, destPath, true);
                    Log($"   [COPY] Staged activator to {destPath}");

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = destPath,
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = destDir
                    });

                    Log("   [SUCCESS] Activator launched in new window.");
                    Log("   [NOTE] Cleanup C:\\PG_Activator manually if needed.");
                }
                else
                {
                    Log("   [ERROR] Activator script (activators\\MAS_AIO.cmd) not found.");
                }
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Failed to launch activator: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                NotifyCommands();
                Log("------------------------------------------------");
            }
        }

        private async Task RunScriptTask(
            string scriptName,
            string description,
            string? altName = null
        )
        {
            if (IsBusy) return;
            IsBusy = true;
            NotifyCommands();

            Log("------------------------------------------------");
            Log(description);

            try
            {
                // Lazy on-demand extraction for individual script execution
                await ExtractSpecificFile(null, $"*{Path.GetFileName(scriptName)}*");
                if (!string.IsNullOrEmpty(altName))
                {
                    await ExtractSpecificFile(null, $"*{Path.GetFileName(altName)}*");
                }

                string? scriptPath = ResolveAssetPath(scriptName);
                if ((string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath)) && !string.IsNullOrEmpty(altName))
                {
                    string? altPath = ResolveAssetPath(altName);
                    if (!string.IsNullOrEmpty(altPath) && File.Exists(altPath))
                    {
                        scriptPath = altPath;
                        Log($"   [INFO] '{scriptName}' not found. Using '{altName}' instead.");
                    }
                }

                if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
                {
                    if (scriptPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                    {
                        await RunProcessAsync(
                            "powershell.exe",
                            $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                            description
                        );
                    }
                    else
                    {
                        await RunProcessAsync("cmd.exe", $"/c \"{scriptPath}\"", description);
                    }

                    Log("   [SUCCESS] Operation complete.");
                }
                else
                {
                    Log($"   [ERROR] Script not found: {scriptName}");
                }
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Execution Failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                NotifyCommands();
                Log("------------------------------------------------");
            }
        }

        private void NotifyCommands()
        {
            RunActivatorCommand.NotifyCanExecuteChanged();
            RunDebloatCommand.NotifyCanExecuteChanged();
            RunDefenderCommand.NotifyCanExecuteChanged();
            InstallCommand.NotifyCanExecuteChanged();
        }
    }
}

