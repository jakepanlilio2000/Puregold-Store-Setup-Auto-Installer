using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallTreasuryPackage()
        {
            await InstallCommonPackages();
            await InstallMMS();
        }
    }
}