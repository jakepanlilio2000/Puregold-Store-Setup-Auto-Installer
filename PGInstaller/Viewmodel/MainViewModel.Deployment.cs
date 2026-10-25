using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PGInstaller.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Windows;

namespace PGInstaller.Viewmodel;
partial class MainViewModel
{
    [ObservableProperty]
    private bool _disableFirewall;
    [ObservableProperty]
    private bool _setWallpaper;
    [ObservableProperty]
    private bool _setPowerOptions;
    [ObservableProperty]
    private bool _configureTaskbar;
    [ObservableProperty]
    private bool _optimizeWindows;
    [ObservableProperty]
    private string _preflightSummary = "Check readiness before installation.";
    [ObservableProperty]
    private int _selectedWorkspaceIndex;
    [ObservableProperty]
    private bool _hasSavedDeployment;
    [ObservableProperty]
    private string _resumeHint = "";
    public ObservableCollection<PreflightItem> PreflightItems { get; } = [];
    public ObservableCollection<PreflightItem> DeploymentResults { get; } = [];

    private InstallJournal? _journal;
    private InstallJournal Journal => _journal ??= new InstallJournal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PGInstaller", "install-state.json"));

    private DeploymentState? _savedDeployment;
    private string _bootId = "unknown";
    private bool _packageRestartRequired;
    private bool _forceCurrentPackage;
    private string? _packageError;
    private bool _applyingSettings;
    private bool _settingsHadErrors;
    private WindowsOptions CurrentWindowsOptions() => new()
    {
        DisableFirewall = DisableFirewall,
        SetWallpaper = SetWallpaper,
        SetPowerOptions = SetPowerOptions,
        ConfigureTaskbar = ConfigureTaskbar,
        OptimizeWindows = OptimizeWindows,
        CreateRestorePoint = IsRestorePointEnabled,
        JoinDomain = JoinDomainAfterInstall
    };
    private void RestoreWindowsOptions(WindowsOptions options)
    {
        DisableFirewall = options.DisableFirewall;
        SetWallpaper = options.SetWallpaper;
        SetPowerOptions = options.SetPowerOptions;
        ConfigureTaskbar = options.ConfigureTaskbar;
        OptimizeWindows = options.OptimizeWindows;
        IsRestorePointEnabled = options.CreateRestorePoint;
        JoinDomainAfterInstall = options.JoinDomain;
    }

    private List<string> SelectedApps() => PreviewList.Where(x => (!x.IsInstalled && x.IsChecked) || (x.IsInstalled && x.ForceInstall)).Select(x => x.Name).ToList();
    private static string ReadBootId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            using var systems = searcher.Get();
            foreach (ManagementObject system in systems)
                using (system)
                    return system["LastBootUpTime"]?.ToString() ?? "unknown";
        }
        catch (Exception)
        {
        }

        return "unknown";
    }

    private void RefreshSavedDeployment()
    {
        try
        {
            _savedDeployment = Journal.Load();
            if (_savedDeployment != null && _savedDeployment.RebootPending && _savedDeployment.BootId != "unknown" && _bootId != "unknown" && _savedDeployment.BootId != _bootId)
            {
                _savedDeployment.RebootPending = false;
                Journal.Save(_savedDeployment);
            }

            if (_savedDeployment != null)
                UpdateDeploymentResults(_savedDeployment);
            HasSavedDeployment = _savedDeployment != null && (_savedDeployment.HasPending || !_savedDeployment.SettingsApplied || _savedDeployment.Packages.Any(p => p.Status is PackageStatus.Failed or PackageStatus.Blocked) || _savedDeployment.RebootPending);
            ResumeHint = !HasSavedDeployment ? "" : _savedDeployment!.RebootPending && !_savedDeployment.CanResume(_bootId) ? "Restart Windows, then reopen Store Setup to continue." : $"Saved {_savedDeployment!.Department} deployment: {_savedDeployment.Packages.Count(p => p.Status is PackageStatus.Pending or PackageStatus.InProgress)} pending, " + $"{_savedDeployment.Packages.Count(p => p.Status is PackageStatus.Failed or PackageStatus.Blocked)} failed or unavailable.";
        }
        catch (Exception ex)
        {
            _savedDeployment = null;
            HasSavedDeployment = false;
            ResumeHint = "Saved progress could not be read.";
            Log($"   [WARN] {ex.Message}");
        }

        ResumeDeploymentCommand.NotifyCanExecuteChanged();
        RetryFailedCommand.NotifyCanExecuteChanged();
        RetryPackageCommand.NotifyCanExecuteChanged();
    }

    private bool CanResumeDeployment() => !IsBusy && _savedDeployment?.CanResume(_bootId) == true;
    private bool CanRetryFailed() => !IsBusy && _savedDeployment != null && !_savedDeployment.RebootPending && _savedDeployment.Packages.Any(p => p.Status is PackageStatus.Failed or PackageStatus.Blocked);
    [RelayCommand(CanExecute = nameof(CanRunTool))]
    private async Task CheckReadiness()
    {
        IsBusy = true;
        _checkInstalledCts?.Cancel();
        CurrentTaskDescription = "Checking readiness...";
        try
        {
            var report = await CheckReadinessCore(SelectedApps());
            SelectedWorkspaceIndex = 1;
            CurrentTaskDescription = report.CanProceed ? "Readiness checked — review the report" : "Resolve readiness issues before installing";
        }
        catch (Exception ex)
        {
            PreflightSummary = $"Readiness check failed: {ex.Message}";
            Log($"   [ERROR] {PreflightSummary}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<PreflightReport> CheckReadinessCore(List<string> selection, bool allowEmpty = false)
    {
        Assets.ResetVerification();
        using var identity = WindowsIdentity.GetCurrent();
        var administrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        using var inProgress = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\InProgress");
        long free = new DriveInfo(Path.GetPathRoot(@"C:\Assets")!).AvailableFreeSpace;
        var config = new List<string>();
        if (SetWallpaper)
            config.Add("PG-wallpaper.jpeg");
        if (ConfigureTaskbar)
            config.Add("Pin-Taskbar.ps1");
        var report = await PreflightService.CheckAsync(selection, SelectedDepartment, Assets, administrator, OperatingSystem.IsWindowsVersionAtLeast(10) && Environment.Is64BitOperatingSystem, free, inProgress != null, PreviewList.Where(p => p.IsInstalled).Select(p => p.Name).ToArray(), config, allowEmpty);
        PreflightItems.Clear();
        foreach (var item in report.Items)
            PreflightItems.Add(item);
        PreflightSummary = $"{report.ReadyPackages.Count} ready; {report.Items.Count(i => i.IsBlocking)} blocking issue(s).";
        foreach (var item in report.Items)
            Log($"   [PREFLIGHT] {item.Name}: {item.Status} — {item.Details}");
        return report;
    }

    [RelayCommand(CanExecute = nameof(CanResumeDeployment))]
    private async Task ResumeDeployment()
    {
        var state = _savedDeployment;
        if (state == null || !CanResumeDeployment())
            return;
        if (Assets.CatalogFingerprint != state.CatalogFingerprint)
        {
            Log("   [ERROR] The asset catalog changed. Start a new selection or explicitly retry failed packages using the new release.");
            return;
        }

        await RunDeployment(state.Packages.Where(p => p.Status is PackageStatus.Pending or PackageStatus.InProgress).Select(p => p.Name).ToList(), state);
    }

    [RelayCommand(CanExecute = nameof(CanRetryFailed))]
    private async Task RetryFailed()
    {
        var state = _savedDeployment;
        if (state == null || !CanRetryFailed())
            return;
        var names = state.Packages.Where(p => p.Status is PackageStatus.Failed or PackageStatus.Blocked).Select(p => p.Name).ToList();
        foreach (var package in state.Packages.Where(p => names.Contains(p.Name)))
            package.Force = true;
        await RunDeployment(names, state);
    }

    private bool CanRetryPackage(string? name) => CanRetryFailed() && _savedDeployment!.Packages.Any(p => p.Name == name && p.Status is PackageStatus.Failed or PackageStatus.Blocked);
    [RelayCommand(CanExecute = nameof(CanRetryPackage))]
    private async Task RetryPackage(string? name)
    {
        if (!CanRetryPackage(name))
            return;
        var state = _savedDeployment!;
        state.Packages.Single(p => p.Name == name).Force = true;
        await RunDeployment([name!], state);
    }

    private async Task RunDeployment(List<string> selection, DeploymentState? state = null)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        _checkInstalledCts?.Cancel();
        LogOutput = "";
        CurrentTaskDescription = "Checking readiness...";
        ProgressPercentage = 0;
        bool completed = false;
        bool checkpointSaved = false;
        try
        {
            if (state != null)
            {
                SelectedDepartment = state.Department;
                _checkInstalledCts?.Cancel();
                RestoreWindowsOptions(state.Options);
            }

            var report = await CheckReadinessCore(selection, state != null && selection.Count == 0);
            SelectedWorkspaceIndex = 1;
            if (report.HasSystemBlocker)
            {
                CurrentTaskDescription = "Resolve readiness issues before installing";
                return;
            }

            if (report.ReadyPackages.Count != selection.Count && report.ReadyPackages.Count > 0 && MessageBox.Show($"{selection.Count - report.ReadyPackages.Count} selected package(s) are unavailable. See Readiness for details.\n\nInstall the {report.ReadyPackages.Count} ready package(s)?", "Readiness check", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            state ??= new DeploymentState
            {
                Department = SelectedDepartment ?? "IT",
                Options = CurrentWindowsOptions(),
                Packages = selection.Select(name => new PackageCheckpoint { Name = name, Force = PreviewList.Any(p => p.Name == name && p.ForceInstall) }).ToList()
            };
            state.CatalogFingerprint = Assets.CatalogFingerprint!;
            state.RebootPending = false;
            state.BootId = _bootId;
            _savedDeployment = state;
            StartNewStagingSession();
            _assetsPath = @"C:\Assets";
            foreach (var name in selection)
            {
                var package = state.Packages.Single(p => p.Name == name);
                package.Status = report.ReadyPackages.Contains(name) ? PackageStatus.Pending : PackageStatus.Blocked;
                package.Error = report.Items.FirstOrDefault(i => i.Package == name && i.IsBlocking)?.Details;
                if (package.Status == PackageStatus.Blocked)
                    continue;
                CurrentTaskDescription = $"Verifying {name}";
                foreach (var alternatives in PackageAssets.For(name, state.Department))
                {
                    bool valid = false;
                    foreach (var asset in alternatives)
                        if (await EnsureAssetAsync(asset) != null)
                        {
                            valid = true;
                            break;
                        }

                    if (!valid)
                    {
                        package.Status = PackageStatus.Blocked;
                        package.Error = "Package could not be extracted or failed SHA-256 verification: " + string.Join(" or ", alternatives);
                        PreflightItems.Add(new(name, "Blocked", package.Error, true, name));
                        break;
                    }
                }
            }

            foreach (var asset in new[]
            {
                SetWallpaper ? "PG-wallpaper.jpeg" : null,
                ConfigureTaskbar ? "Pin-Taskbar.ps1" : null
            }.OfType<string>())
                if (await EnsureAssetAsync(asset) == null)
                    throw new InvalidDataException($"Selected Windows setting asset failed verification: {asset}");
            Journal.Save(state);
            checkpointSaved = true;
            UpdateDeploymentResults(state);
            var ready = selection.Where(name => state.Packages.Single(p => p.Name == name).Status == PackageStatus.Pending).ToList();
            if (ready.Count == 0 && selection.Count != 0)
            {
                CurrentTaskDescription = "No selected packages passed verification";
                return;
            }

            CurrentStep = 0;
            TotalSteps = ready.Count + 1;
            ProgressPercentage = 0;
            if (!await HandleComputerRenameAsync() || !await HandleDomainJoinAsync())
                return;
            if (state.Options.CreateRestorePoint && !state.RestorePointCreated)
            {
                await CreateSystemRestorePoint();
                state.RestorePointCreated = true;
                Journal.Save(state);
            }

            await InstallSelectedApplications(state, ready);
            if (state.RebootPending)
            {
                CurrentTaskDescription = "Restart required — reopen Store Setup to resume";
                Log("   [RESTART] Progress saved. Restart Windows and reopen Store Setup to continue the pending queue.");
                return;
            }

            if (!state.SettingsApplied && !state.HasPending)
            {
                state.SettingsApplied = await ApplySelectedWindowsSettings(state.Packages.Select(p => p.Name), state.Options);
                Journal.Save(state);
            }

            IncrementProgress();
            completed = !state.HasPending && state.SettingsApplied;
            CurrentTaskDescription = completed ? (InstallFailCount == 0 ? "Installation completed" : "Completed — retry failed packages when ready") : state.HasPending ? "Pending packages remain — use Resume" : "Windows settings need attention — use Resume";
            if (completed)
                ProgressPercentage = 100;
        }
        catch (Exception ex)
        {
            CurrentTaskDescription = "Installation stopped — progress saved when available";
            Log($"   [ERROR] {ex.Message}");
        }
        finally
        {
            _isRunningPackage = false;
            IsBusy = false;
            if (checkpointSaved)
                RefreshSavedDeployment();
            if (completed)
                ShowInstallationSummary();
            await CheckInstalledSoftwareAsync();
        }
    }

    private void UpdateDeploymentResults(DeploymentState state)
    {
        DeploymentResults.Clear();
        foreach (var package in state.Packages)
            DeploymentResults.Add(new(package.Name, package.Status.ToString(), $"Attempts: {package.Attempts}. " + (package.Error ?? ""), package.Status is PackageStatus.Failed or PackageStatus.Blocked, package.Name));
        InstallSuccessCount = state.Packages.Count(p => p.Status == PackageStatus.Succeeded);
        InstallSkipCount = state.Packages.Count(p => p.Status == PackageStatus.Skipped);
        InstallFailCount = state.Packages.Count(p => p.Status is PackageStatus.Failed or PackageStatus.Blocked);
    }

    private async Task<bool> ApplySelectedWindowsSettings(IEnumerable<string> selectedApps, WindowsOptions options)
    {
        _applyingSettings = true;
        _settingsHadErrors = false;
        try
        {
            if (options.OptimizeWindows)
                ApplySystemOptimizations();
            if (options.DisableFirewall)
                await RunProcessAsync("netsh", "advfirewall set allprofiles state off", "Disabling Windows Firewall");
            if (options.SetWallpaper)
                await ApplyWallpaper();
            if (options.ConfigureTaskbar)
            {
                await ClearTaskbar();
                await PinToTaskbar("File Explorer", "explorer.exe");
                if (selectedApps.Contains("Google Chrome"))
                    await PinToTaskbar("Google Chrome", "chrome.exe");
                if (selectedApps.Contains("Mozilla Firefox"))
                    await PinToTaskbar("Mozilla Firefox", "firefox.exe");
                if (selectedApps.Contains("Mozilla Thunderbird"))
                    await PinToTaskbar("Mozilla Thunderbird", "thunderbird.exe");
            }

            if (options.SetPowerOptions)
                foreach (var setting in new[]
                {
                    "standby-timeout-ac",
                    "standby-timeout-dc",
                    "monitor-timeout-ac",
                    "monitor-timeout-dc"
                }

                )
                    await RunProcessAsync("powercfg", $"/change {setting} 0", "Applying power setting " + setting);
            return !_settingsHadErrors;
        }
        finally
        {
            _applyingSettings = false;
        }
    }
}
