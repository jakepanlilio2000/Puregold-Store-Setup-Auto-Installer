using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallStoreOperationsPackage(string role)
        {
            await InstallCommonPackages();
            await SmartInstall("VLC Media Player", "vlc.exe", "/S", "VLC");
            await InstallMMS();

            await InstallPIMS();
            switch (role)
            {
                case "Manager":
                    await SmartInstall("Zoom", "ZoomInstaller.exe", "/silent", "Zoom");
                    break;

                case "Customer Service":
                    await InstallBartender();
                    break;

                case "Selling":
                    break;

                case "HBC":
                    break;
            }

            Log($"   [SUCCESS] Store Operations ({role}) setup complete.");
        }
    }
}
