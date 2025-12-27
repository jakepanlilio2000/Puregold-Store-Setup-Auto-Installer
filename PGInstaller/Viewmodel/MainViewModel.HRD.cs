using System.IO;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallHRDPackage()
        {
            bool success = await RunProcessAsync(
                "dism",
                "/online /enable-feature /featurename:NetFX3 /all /NoRestart",
                "Enabling .NET Framework 3.5"
            );

            if (!success)
            {
                Log("   [FALLBACK] Standard enable failed. Attempting to install from offline CABs...");

                string netfxCab = Path.Combine(_assetsPath, "netfx3.cab");
                if (File.Exists(netfxCab))
                {
                    await RunProcessAsync(
                        "dism",
                        $"/online /add-package /packagepath:\"{netfxCab}\" /NoRestart",
                        "Installing .NET 3.5 (Offline CAB)"
                    );
                }
                else
                {
                    Log($"   [ERROR] Fallback failed: netfx3.cab not found in {_assetsPath}");
                }

                string ieCab = Path.Combine(_assetsPath, "iex.cab");
                if (File.Exists(ieCab))
                {
                    await RunProcessAsync(
                        "dism",
                        $"/online /add-package /packagepath:\"{ieCab}\" /NoRestart",
                        "Installing Internet Explorer (Offline CAB)"
                    );
                }
                else
                {
                    Log($"   [ERROR] Fallback failed: iex.cab not found in {_assetsPath}");
                }
            }

            await InstallCommonPackages();
        }
    }
}