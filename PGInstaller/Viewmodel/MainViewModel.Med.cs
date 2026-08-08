using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        [ObservableProperty]
        private string? _selectedMedicineName;

        public Dictionary<string, string> MedicineMap { get; } = new Dictionary<string, string>
        {
            { "Windows/Office Activator", "MAS_AIO.cmd" },
            { "CorelDRAW Keygen", "cx5.exe" },
            { "Adobe Patcher", "GenP.exe" },
            { "Bartender 2022 Patcher", "bp2022p.exe" },
            { "Bartender 2016 Patcher", "bp2016p.exe" },
            { "Bartender 10.1 Patcher", "bp10.1p.exe" },
        };

        public ObservableCollection<string> MedicineList => new(MedicineMap.Keys);

        [RelayCommand]
        private async Task RunMedicine()
        {
            if (string.IsNullOrEmpty(SelectedMedicineName)) return;

            if (MedicineMap.TryGetValue(SelectedMedicineName, out string? fileName))
            {
                string fullPath = Path.Combine(_assetsPath!, "Activators", fileName);

                if (File.Exists(fullPath))
                {
                    Log($"   [LAUNCH] Opening {SelectedMedicineName}...");

                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fullPath,
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetDirectoryName(fullPath)
                        });
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Failed to launch: {ex.Message}");
                    }
                }
                else
                {
                    Log($"   [ERROR] File not found: Activators\\{fileName}");
                }
            }
            else
            {
                Log($"   [ERROR] No file mapped for: {SelectedMedicineName}");
            }

            await Task.CompletedTask;
        }
    }
}