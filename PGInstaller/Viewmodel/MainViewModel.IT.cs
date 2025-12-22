using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallITPackage()
        {
            await InstallCommonPackages();
            await SmartInstall("Zoom", "ZoomInstaller.exe", "/silent", "Zoom");
            await SmartInstall("Advance IP Scanner", "ipscanner.exe", checkName: "Advanced IP Scanner");
            await SmartInstall("PuTTY", "putty.msi", "/qn", "PuTTY");
            string regFile = "putty_settings.reg";
            string? tempRegPath = await ExtractFileFromZipToTemp(regFile);
            if (!string.IsNullOrEmpty(tempRegPath))
            {
                await RunProcessAsync("reg", $"import \"{tempRegPath}\"", "Applying PuTTY Settings");
                if (tempRegPath.StartsWith(Path.GetTempPath())) File.Delete(tempRegPath);
            }

            await InstallMsiFromAssetsOrZip("radmins.msi", "/qn /norestart", "Radmin Server", "Radmin Server 3.5");
            string pbrFile = "radmin.pbr";
            string? pbrPath = await ExtractFileFromZipToTemp(pbrFile);
            if (!string.IsNullOrEmpty(pbrPath))
            {
                string destPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Radmin", "radmin.pbr");
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(pbrPath, destPath, true);
                    Log("   [SUCCESS] Radmin configuration applied.");
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to copy Radmin config: {ex.Message}");
                }
                if (pbrPath.StartsWith(Path.GetTempPath())) File.Delete(pbrPath);
            }
            string batFile = "install.bat";
            string dll1 = "newtstop.dll";
            string dll2 = "nts64helper.dll";

            string? batPath = await ExtractFileFromZipToTemp(batFile);
            string? dll1Path = await ExtractFileFromZipToTemp(dll1);
            string? dll2Path = await ExtractFileFromZipToTemp(dll2);

            if (!string.IsNullOrEmpty(batPath))
            {
                await RunProcessAsync("cmd.exe", $"/c \"{batPath}\"", "Running Post-Install Script (install.bat)");
                try
                {
                    if (batPath.StartsWith(Path.GetTempPath())) File.Delete(batPath);
                    if (!string.IsNullOrEmpty(dll1Path) && dll1Path.StartsWith(Path.GetTempPath())) File.Delete(dll1Path);
                    if (!string.IsNullOrEmpty(dll2Path) && dll2Path.StartsWith(Path.GetTempPath())) File.Delete(dll2Path);
                }
                catch { }
            }
            else
            {
                Log("   [WARNING] install.bat not found.");
            }
        }

        private async Task<string?> ExtractFileFromZipToTemp(string filename)
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", filename);
            if (File.Exists(localPath)) return localPath;

            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "assets.zip");
            if (File.Exists(zipPath))
            {
                try
                {
                    return await Task.Run(() =>
                    {
                        using ZipArchive archive = ZipFile.OpenRead(zipPath);
                        var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals(filename, StringComparison.OrdinalIgnoreCase));
                        if (entry != null)
                        {
                            string tempPath = Path.Combine(Path.GetTempPath(), filename);
                            entry.ExtractToFile(tempPath, true);
                            Log($"   [EXTRACT] Extracted {filename} to Temp.");
                            return tempPath;
                        }
                        return null;
                    });
                }
                catch (Exception ex)
                {
                    Log($"   [ERROR] Zip extraction failed for {filename}: {ex.Message}");
                    return null;
                }
            }
            return null;
        }
        private async Task InstallMsiFromAssetsOrZip(string msiName, string args, string description, string? checkName = null)
        {
            if (!string.IsNullOrEmpty(checkName) && IsAppInstalled(checkName))
            {
                Log($"   [SKIP] {description} is already installed.");
                return;
            }

            string? msiPath = await ExtractFileFromZipToTemp(msiName);
            if (!string.IsNullOrEmpty(msiPath))
            {
                await RunProcessAsync("msiexec.exe", $"/i \"{msiPath}\" {args}", $"Installing {description}");
                if (msiPath.StartsWith(Path.GetTempPath()))
                {
                    try { File.Delete(msiPath); } catch { }
                }
            }
            else
            {
                Log($"   [SKIP] {description} installer not found ({msiName}).");
            }
        }
    }
}