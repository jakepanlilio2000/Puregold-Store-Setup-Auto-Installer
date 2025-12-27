using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
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
                Log("   [INIT] Windows Defender Detected. Disabler button enabled.");
            else
                Log("   [INIT] Windows Defender NOT detected. Disabler button disabled.");
        }

        private bool CanRunDefender() => IsDefenderPresent && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanRunDefender))]
        private async Task RunDefender()
        {
            if (IsBusy) return;
            IsBusy = true;
            RunDefenderCommand.NotifyCanExecuteChanged(); 

            Log("------------------------------------------------");
            Log("Attempting to Disable Windows Defender...");

            try
            {
                await PrepareAssets();

                string scriptPath = Path.Combine(_assetsPath, "AchillesScript.cmd");

                if (File.Exists(scriptPath))
                {
                    await RunProcessAsync("cmd.exe", $"/c \"{scriptPath}\" apply 4", "Running Achilles (Defender Disabler)");
                    Log("   [SUCCESS] Script executed.");

                    await CheckDefender();
                }
                else
                {
                    Log($"   [ERROR] Script not found at: {scriptPath}");
                }
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Operation Failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RunDefenderCommand.NotifyCanExecuteChanged();
                Log("------------------------------------------------");
            }
        }
    }
}