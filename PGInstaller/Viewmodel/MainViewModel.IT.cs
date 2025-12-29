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
            await SmartInstall("WinSCP", "WinSCP.exe", "/VERYSILENT /NORESTART /ALLUSERS", "WinSCP");
            await InstallPutty();
            await InstallRadminViewer();
            await InstallMMS();
            await InstallPIMS();
        }
    }
}