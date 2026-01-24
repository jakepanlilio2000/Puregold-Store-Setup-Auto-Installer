using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallICDPackage()
        {
            await InstallCommonPackages();
            await InstallMMS();
            await InstallWampServer();

            await PasteVARIANCE();
            await InstallInventoryTools();
            await InstallNetFx3();
            await InstallPIMS();
        }
    }
}
