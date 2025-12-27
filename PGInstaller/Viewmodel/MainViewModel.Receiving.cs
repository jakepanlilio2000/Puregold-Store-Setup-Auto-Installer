using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallReceivingPackage()
        {
            await InstallCommonPackages();
            await SmartInstall("Bartender", "bartender.exe", "/silent /S /I", "Bartender");
            await InstallMMS();
            await PasteMMS();
        }
    }
}
