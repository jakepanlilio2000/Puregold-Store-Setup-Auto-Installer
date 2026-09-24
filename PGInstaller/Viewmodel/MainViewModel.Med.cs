using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
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
            { "Coreldraw Graphics X5 Keygen", "cx5.exe" },
            { "Coreldraw Graphics X7 Keygen", "cx7.exe" },
            { "Adobe Patcher", "GenP.exe" },
            { "Bartender 2022 Patcher", "bp2022p.exe" },
            { "Bartender 2016 Patcher", "bt2016p.exe" },
            { "Bartender 10.1 Patcher", "bp10.1p.exe" },
        };

        public ObservableCollection<string> MedicineList => new(MedicineMap.Keys);

        [RelayCommand]
        private async Task RunMedicine()
        {
            if (string.IsNullOrEmpty(SelectedMedicineName)) return;

            if (MedicineMap.TryGetValue(SelectedMedicineName, out string? fileName))
            {
                string relativePath = Path.Combine("activators", fileName);
                string? fullPath = ResolveAssetPath(relativePath) ?? ResolveAssetPath(fileName);

                // Fallback for bt2016p.exe / bp2016p.exe spelling variation
                if ((string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) && fileName == "bt2016p.exe")
                {
                    fullPath = ResolveAssetPath(Path.Combine("activators", "bp2016p.exe")) ?? ResolveAssetPath("bp2016p.exe")
                               ?? ResolveAssetPath(Path.Combine("activators", "bt2016.exe")) ?? ResolveAssetPath("bt2016.exe");
                }

                // Lazy on-demand extraction if file does not yet exist in Assets
                if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                {
                    await ExtractSpecificFile(null, $"*{fileName}*");
                    fullPath = ResolveAssetPath(relativePath) ?? ResolveAssetPath(fileName);
                    if ((string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) && fileName == "bt2016p.exe")
                    {
                        await ExtractSpecificFile(null, "*bp2016*");
                        fullPath = ResolveAssetPath(Path.Combine("activators", "bp2016p.exe")) ?? ResolveAssetPath("bp2016p.exe")
                                   ?? ResolveAssetPath(Path.Combine("activators", "bt2016.exe")) ?? ResolveAssetPath("bt2016.exe");
                    }
                }

                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                {
                    Log($"   [LAUNCH] Opening {SelectedMedicineName}...");

                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = fullPath,
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetDirectoryName(fullPath)
                        };

                        if (fullPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                            fullPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                        {
                            startInfo.Verb = "runas";
                        }

                        Process.Start(startInfo);
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Failed to launch: {ex.Message}");
                    }
                }
                else
                {
                    Log($"   [ERROR] File not found: activators\\{fileName}");
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

