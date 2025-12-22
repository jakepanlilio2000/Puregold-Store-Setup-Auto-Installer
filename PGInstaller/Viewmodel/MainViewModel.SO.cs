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
                await SmartInstall (
                    "Microsoft Teams",
                    "TeamsSetup.exe"
                );
            if (role == "Customer Service")
                await SmartInstall("Zoom", "ZoomInstaller.exe", "/silent");
            if (role == "Gcash")
                await SmartInstall("GitHub CLI", "gh_cli.msi", "/qn");
            if (role == "HBC")
                await SmartInstall("Slack", "SlackSetup.exe", "/silent");
        }
    }
}
