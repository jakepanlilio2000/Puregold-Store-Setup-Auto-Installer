namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallCreativePackage()
        {
            await InstallCommonPackages();
            await InstallCorelPSIllu();
        }
    }
}