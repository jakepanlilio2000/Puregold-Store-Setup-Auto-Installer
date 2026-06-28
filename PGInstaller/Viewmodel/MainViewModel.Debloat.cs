using CommunityToolkit.Mvvm.Input;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        [RelayCommand(CanExecute = nameof(CanRunTool))]
        private async Task RunDebloat()
        {
            await RunScriptTask("debloat.bat", "Debloating Windows...");
        }
    }
}