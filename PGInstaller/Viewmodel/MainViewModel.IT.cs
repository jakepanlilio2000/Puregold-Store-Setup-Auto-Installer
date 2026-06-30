namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task InstallITPackage()
        {
            await InstallCommonPackages();
            await SmartInstall("Zoom", "ZoomInstaller.exe", "/silent", "Zoom");
            await SmartInstall("Advance IP Scanner", "ipscanner.exe", checkName: "Advanced IP Scanner");
            await SmartInstall("PITK", "PITK Setup.exe", "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", "PITK");
            await InstallAVGW();
            await InstallPutty();
            await InstallWinSCP();
            await InstallRadminViewer();
            await InstallMMS();
            await InstallPIMS();
            await RunChromeBookmarkScript();
        }
    }
}