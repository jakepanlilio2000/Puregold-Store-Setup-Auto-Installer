using PGInstaller.Services;

namespace PGInstaller.Viewmodel;
partial class MainViewModel
{
    private bool _isRunningPackage;
    private bool _packageHadErrors;
    private bool _packageSkipped;
    private async Task InstallSelectedApplications(DeploymentState state, List<string> selectedApps)
    {
        var ordered = selectedApps.OrderBy(name => name switch
        {
            "All VC++ Redistributables" => 0,
            ".NET Framework 3.5" => 1,
            "Wamp 1.7.2" or "Wamp 2" or "Wamp 2.5" or "Wampserver 3.4.0" => 2,
            _ => 3
        }).ToArray();
        await InstallationRunner.RunAsync(state, ordered, _bootId, async package =>
        {
            UpdateDeploymentResults(state);
            _packageHadErrors = false;
            _packageSkipped = false;
            _packageRestartRequired = false;
            _packageError = null;
            _forceCurrentPackage = package.Force || package.Attempts > 1;
            _isRunningPackage = true;
            CurrentTaskDescription = $"Installing {package.Name}";
            try
            {
                await InstallSelectedApplication(package.Name);
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] {package.Name}: {ex.Message}");
            }
            finally
            {
                _isRunningPackage = false;
                _forceCurrentPackage = false;
            }

            return new PackageRunResult(!_packageHadErrors, _packageSkipped && !_packageHadErrors, _packageRestartRequired, _packageError);
        }, () => Journal.Save(state), () =>
        {
            UpdateDeploymentResults(state);
            IncrementProgress();
        });
    }

    private async Task InstallSelectedApplication(string name)
    {
        string[] selection = [name];
        switch (name)
        {
            case "Zoom":
                await SmartInstall(name, "ZoomInstaller.exe", "/silent", name);
                break;
            case "Advanced IP Scanner":
                await SmartInstall(name, "ipscanner.exe", "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", name);
                break;
            case "PITK":
                await InstallPITK();
                break;
            case "A&VGW":
                await InstallAVGW();
                break;
            case "PuTTY":
                await InstallPutty();
                break;
            case "WinSCP":
                await InstallWinSCP();
                break;
            case "Radmin Viewer":
                await InstallRadminViewer();
                break;
            case "MMS (PCOMM)":
                await InstallMMS();
                break;
            case "PIMS":
                await InstallPIMS();
                break;
            case "Chrome Bookmarks (CBM)":
                await RunChromeBookmarkScript();
                break;
            case ".NET Framework 3.5":
                await InstallNetFx3();
                break;
            case "FSDM":
                await InstallFSDM();
                break;
            case "Wamp 1.7.2":
            case "Wamp 2":
            case "Wamp 2.5":
            case "Wampserver 3.4.0":
                await InstallWampVersion(selection);
                if (name == "Wamp 2" && SelectedDepartment == "HRD" && !_packageHadErrors)
                    await DeployHrdWampContent();
                break;
            case "Inventory Tools":
                await InstallInventoryTools();
                break;
            case "Variance":
                await PasteVARIANCE();
                break;
            case "Bartender 10.1":
            case "Bartender 2016":
            case "Bartender 2022":
                await InstallBartender(selection);
                break;
            case "Argox Driver":
            case "Zebra Driver":
                await InstallBartenderDrivers(selection);
                break;
            case "Coreldraw Graphics X5":
            case "Coreldraw Graphics X7":
            case "Photoshop CS6":
            case "Illustrator CS6":
                await InstallCorelPSIllu(selection);
                break;
            case "VLC Media Player":
                await SmartInstall(name, "vlc.exe", "/S", "VLC");
                break;
            case "Oracle Java Runtime":
                var installer = await EnsureAssetAsync("jre.exe") ?? await EnsureAssetAsync("java.exe");
                if (installer == null)
                    throw new InvalidOperationException("Java installer is missing.");
                await SmartInstall(name, installer, "/s", "Java");
                break;
            default:
                await InstallCommonPackages(selection);
                break;
        }
    }
}
