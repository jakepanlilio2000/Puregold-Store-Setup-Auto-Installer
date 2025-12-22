using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        [RelayCommand]
        private async Task RunDebloat()
        {
            if (IsBusy) return;
            IsBusy = true;
            Log("------------------------------------------------");
            Log("Starting Windows Debloat...");

            try
            {
                await PrepareAssets();
                string scriptName = "debloat.bat";
                string? scriptPath = await ExtractFileFromZipToTemp(scriptName);

                if (!string.IsNullOrEmpty(scriptPath))
                {
                    await RunProcessAsync(
                        "cmd.exe",
                        $"/c \"{scriptPath}\"",
                        "Executing Debloat Batch Script"
                    );
                    if (scriptPath.StartsWith(Path.GetTempPath()))
                    {
                        try { File.Delete(scriptPath); } catch { }
                    }
                    Log("   [SUCCESS] Debloat execution finished.");
                }
                else
                {
                    Log($"   [ERROR] Debloat script not found: {scriptName}");
                }
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Debloat Failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                Log("------------------------------------------------");
            }
        }
    }
}
