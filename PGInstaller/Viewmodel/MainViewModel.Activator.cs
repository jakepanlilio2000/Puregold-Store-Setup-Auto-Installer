using System.Diagnostics;
using System.IO;
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
                await PrepareAssets();
                string scriptName = "activator.cmd";
                string sourcePath = Path.Combine(_assetsPath, scriptName);

                if (!File.Exists(sourcePath))
                {
                    scriptName = "win.cmd";
                    sourcePath = Path.Combine(_assetsPath, scriptName);
                }

                if (File.Exists(sourcePath))
                {
                    string destDir = @"C:\PG_Activator";
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
                    Log("   [ERROR] Activator script (activator.cmd or win.cmd) not found.");
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
                await PrepareAssets();

                string scriptPath = Path.Combine(_assetsPath, scriptName);
                if (!File.Exists(scriptPath) && !string.IsNullOrEmpty(altName))
                {
                    string altPath = Path.Combine(_assetsPath, altName);
                    if (File.Exists(altPath))
                    {
                        scriptPath = altPath;
                        Log($"   [INFO] '{scriptName}' not found. Using '{altName}' instead.");
                    }
                }

                if (File.Exists(scriptPath))
                {
                    await RunProcessAsync("cmd.exe", $"/c \"{scriptPath}\"", description);
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