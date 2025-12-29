using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;


namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        [ObservableProperty]
        private string? _selectedMedicineName;

        public Dictionary<string, string> MedicineMap { get; } = new Dictionary<string, string>
        {
            { "Windows Activator", "activator.cmd" },
            { "CorelDRAW Keygen", "CorelKeygen.exe" },
            { "Adobe Patcher", "AdobePatch.exe" },
            { "IDM Crack", "IDM_Crack.exe" },
            { "Office Tool", "OfficeSetup.cmd" }
        };

        public ObservableCollection<string> MedicineList => new ObservableCollection<string>(MedicineMap.Keys);

        [RelayCommand]
        private async Task RunMedicine()
        {
            if (string.IsNullOrEmpty(SelectedMedicineName)) return;

            if (MedicineMap.TryGetValue(SelectedMedicineName, out string? realFileName))
            {
                Log($"   Under Development...");
                //await RunScriptTask(realFileName, $"Launching {SelectedMedicineName} ({realFileName})...");
            }
            else
            {
                Log($"   [ERROR] No file mapped for: {SelectedMedicineName}");
            }
        }

    }
}
