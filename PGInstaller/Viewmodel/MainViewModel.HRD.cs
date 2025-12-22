using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallHRDPackage()
        {
            await RunProcessAsync("dism", "/online /enable-feature /featurename:NetFX3 /all /NoRestart", "Enabling .NET Framework 3.5");
            await InstallCommonPackages();
        }
    }
}
