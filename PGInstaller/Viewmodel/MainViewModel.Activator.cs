using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private bool CanRunTool() => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanRunTool))]
        private async Task RunActivator()
        {
            await RunScriptTask("activator.cmd", "Activating Windows...", "win.cmd");
        }

        private async Task RunScriptTask(string scriptName, string description, string? altName = null)
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

                // Fallback name check
                if (!File.Exists(scriptPath) && !string.IsNullOrEmpty(altName))
                    scriptPath = Path.Combine(_assetsPath, altName);

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
            catch (Exception ex) { Log($"   [ERROR] Failed: {ex.Message}"); }
            finally { IsBusy = false; NotifyCommands(); Log("------------------------------------------------"); }
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
