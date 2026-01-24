using System.IO;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallHRDPackage()
        {
            await InstallNetFx3();
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
                Log($"   [SKIP] iex.cab not found. Skipping IE install.");
            }

            await InstallCommonPackages();
            await InstallPIMS();
            await InstallFSDM();
        }
    }
}