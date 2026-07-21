namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallCreativePackage(IEnumerable<string> selectedApps)
        {
            await InstallCommonPackages(selectedApps);
            if (selectedApps.Contains("Coreldraw Graphics X5") || selectedApps.Contains("Photoshop CS6") || selectedApps.Contains("Illustrator CS6"))
            {
                await InstallCorelPSIllu();
            }
        }
    }
}