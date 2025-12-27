using System.Diagnostics;
using System.IO;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallMMS()
        {
            if (!IsAppInstalled("MMS"))
            {
                await InstallZipPackage("MMS.zip", "setup.exe", "", "MMS System");
            }
        }

        private async Task PasteMMS()
        {
            string mmsSource = Path.Combine(_assetsPath, "MMS.ws");
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string mmsDest = Path.Combine(desktopPath, "MMS.ws");

            if (File.Exists(mmsSource))
            {
                if (!File.Exists(mmsDest))
                {
                    try
                    {
                        File.Copy(mmsSource, mmsDest);
                        Log("   [SUCCESS] Copied MMS.ws to Desktop.");
                    }
                    catch (Exception ex)
                    {
                        Log($"   [ERROR] Failed to copy MMS.ws: {ex.Message}");
                    }
                }
                else
                {
                    Log("   [SKIP] MMS.ws already exists on Desktop.");
                }
            }
            else
            {
                Log("   [WARNING] MMS.ws not found in Assets.");
            }
        }

        private async Task ApplyRadminServer()
        {
            string installBatPath = Path.Combine(_assetsPath, "install.bat");
            if (File.Exists(installBatPath))
            {
                if (File.Exists(Path.Combine(_assetsPath, "newtstop.dll")) &&
                    File.Exists(Path.Combine(_assetsPath, "nts64helper.dll")))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{installBatPath}\"",
                        WorkingDirectory = _assetsPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    await RunCustomProcess(startInfo, "Applying Radmin NewTrialStop Patch");
                }
                else
                {
                    Log("   [ERROR] Dependencies for install.bat (newtstop.dll or nts64helper.dll) missing.");
                }
            }
            else
            {
                Log("   [WARNING] install.bat not found in Assets.");
            }
        }

        private async Task PasteVARIANCE()
        {
            //variance function
        }

        private async Task InstallBartender()
        {

        }
    }
}
