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
            if (role == "Manager")
                await SmartInstall("Zoom", "ZoomInstaller.exe", "/silent", "Zoom");
            await SmartInstall("VLC Media Player", "vlc.exe", "/S", "VLC");
            await InstallMMS();
            await InstallPIMS();
            if (role == "Customer Service")
                await SmartInstall("Bartender", "bartender.exe", "/silent /S /I", "Bartender");
            await SmartInstall("VLC Media Player", "vlc.exe", "/S", "VLC");
            if (role == "Selling")
                await InstallMMS();
            if (role == "HBC")
                await SmartInstall("VLC Media Player", "vlc.exe", "/S", "VLC");
            await InstallMMS();
            await InstallPIMS();
        }
    }
}
