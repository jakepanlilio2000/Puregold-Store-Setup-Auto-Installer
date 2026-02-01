using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    public partial class MainViewModel
    {
        [ObservableProperty]
        private string? _selectedAntivirus;

        public Dictionary<string, string> AntivirusMap { get; } = new Dictionary<string, string>
        {
            { "Symantec Endpoint Protection", "Sep64.msi" },
            { "ESET Endpoint Security", "eset_setup.exe" },
            { "Kaspersky Endpoint", "kes_setup.exe" },
            { "Windows Defender (Enable)", "enable_defender.bat" }
        };

        public ObservableCollection<string> AntivirusList => new(AntivirusMap.Keys);

        [RelayCommand]
        private async Task InstallAntivirus()
        {
            if (string.IsNullOrEmpty(SelectedAntivirus)) return;

            if (AntivirusMap.TryGetValue(SelectedAntivirus, out string? fileName))
            {
                if (fileName.EndsWith(".msi"))
                {
                    await SmartInstall(SelectedAntivirus, fileName, "/qn /norestart", SelectedAntivirus);
                }
                else if (fileName.EndsWith(".bat"))
                {
                    await RunScriptTask(fileName, $"Running {SelectedAntivirus}...");
                }
                else
                {
                    await SmartInstall(SelectedAntivirus, fileName, "/silent", SelectedAntivirus);
                }
            }
            else
            {
                Log($"   [ERROR] Configuration not found for: {SelectedAntivirus}");
            }
        }
    }
}
