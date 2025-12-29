using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunDefenderCommand))]
        private bool _isDefenderPresent;

        private async Task CheckDefender()
        {
            Log("   [INIT] Checking Windows Defender runtime state...");

            var output = await RunProcessCaptureAsync("sc", "query WinDefend");

            bool running =
                output.IndexOf("STATE", StringComparison.OrdinalIgnoreCase) >= 0 &&
                output.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;

            IsDefenderPresent = running;

            if (running)
                Log("   [INIT] Windows Defender running. Disabler enabled.");
            else
                Log("   [INIT] Windows Defender not running. Disabler disabled.");
        }

        private async Task<string> RunProcessCaptureAsync(string file, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            return await p!.StandardOutput.ReadToEndAsync();
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