using CommunityToolkit.Mvvm.ComponentModel;
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
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunDefenderCommand))]
        private bool _isDefenderPresent;
        private async Task CheckDefender()
        {
            Log("   [INIT] Checking Windows Defender status...");
            bool exists = await RunProcessAsync("sc", "query WinDefend", "Checking Defender Service", true);

            IsDefenderPresent = exists;

            if (IsDefenderPresent)
                Log("   [INIT] Windows Defender Detected. Defender Disabler script enabled.");
            else
                Log("   [INIT] Windows Defender NOT detected. Defender Disabler script disabled.");
        }

        [RelayCommand(CanExecute = nameof(CanRunDefender))]
        private async Task RunDefender()
        {
            if (IsBusy) return;
            IsBusy = true;
            RunDefenderCommand.NotifyCanExecuteChanged();

            Log("------------------------------------------------");
            Log("Disabling Windows Defender...");

            try
            {
                await PrepareAssets();
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AchillesScript.cmd");

                if (File.Exists(scriptPath))
                {
                    await RunProcessAsync("cmd.exe", $"/c \"{scriptPath}\" apply 4", "Running Defender Disabler Script");
                    Log("   [SUCCESS] Defender Disabler Script execution finished.");
                }
                else
                {
                    Log($"   [ERROR] Script not found: {scriptPath}");
                }
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Defender Disabler Failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RunDefenderCommand.NotifyCanExecuteChanged();
                Log("------------------------------------------------");
            }
        }

        private bool CanRunDefender() => IsDefenderPresent && !IsBusy;
    }
}
