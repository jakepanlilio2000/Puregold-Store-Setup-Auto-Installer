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

            await PasteVARIANCE();
            await InstallInventoryTools();
            await InstallPIMS();
        }
    }
}
