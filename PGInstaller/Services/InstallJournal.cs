using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace PGInstaller.Services;
internal static class MachineIdentity
{
    public static string Value
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string ?? "name:" + Environment.MachineName;
        }
    }
}

internal enum PackageStatus
{
    Pending,
    InProgress,
    Succeeded,
    Skipped,
    Failed,
    Blocked
}

internal sealed class WindowsOptions
{
    public bool DisableFirewall { get; set; }
    public bool SetWallpaper { get; set; }
    public bool SetPowerOptions { get; set; }
    public bool ConfigureTaskbar { get; set; }
    public bool OptimizeWindows { get; set; }
    public bool CreateRestorePoint { get; set; }
    public bool JoinDomain { get; set; }
}

internal sealed class PackageCheckpoint
{
    public string Name { get; set; } = "";
    public PackageStatus Status { get; set; }
    public bool Force { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

internal sealed class DeploymentState
{
    public string ReleaseVersion { get; set; } = ReleaseInfo.Version;
    public string MachineName { get; set; } = Environment.MachineName;
    [JsonRequired]
    public string MachineId { get; set; } = MachineIdentity.Value;
    public string CatalogFingerprint { get; set; } = "";
    public string Department { get; set; } = "IT";
    public string BootId { get; set; } = "";
    public bool RebootPending { get; set; }
    public bool SettingsApplied { get; set; }
    public bool RestorePointCreated { get; set; }
    public WindowsOptions Options { get; set; } = new();
    public List<PackageCheckpoint> Packages { get; set; } = [];
    public bool HasPending => Packages.Any(p => p.Status is PackageStatus.Pending or PackageStatus.InProgress);

    public bool CanResume(string bootId) => (HasPending || (!SettingsApplied && Packages.Any(p => p.Status is PackageStatus.Succeeded or PackageStatus.Skipped))) && (!RebootPending || (!string.IsNullOrWhiteSpace(BootId) && !string.IsNullOrWhiteSpace(bootId) && BootId != "unknown" && bootId != "unknown" && BootId != bootId));
}

internal sealed class InstallJournal(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    public DeploymentState? Load()
    {
        if (!File.Exists(path))
            return null;
        DeploymentState? state;
        try
        {
            state = JsonSerializer.Deserialize<DeploymentState>(File.ReadAllText(path));
        }
        catch (JsonException)when (File.Exists(path + ".bak"))
        {
            state = JsonSerializer.Deserialize<DeploymentState>(File.ReadAllText(path + ".bak"));
        }

        if (state == null || state.MachineId != MachineIdentity.Value || state.ReleaseVersion != ReleaseInfo.Version || state.Packages.Any(p => !PackageAssets.Required.ContainsKey(p.Name) || !Enum.IsDefined(p.Status) || p.Attempts < 0) || state.Packages.Select(p => p.Name).Distinct().Count() != state.Packages.Count)
            throw new InvalidDataException("The saved deployment does not match this computer or application version.");
        foreach (var package in state.Packages.Where(p => p.Status == PackageStatus.InProgress))
            package.Status = PackageStatus.Pending;
        return state;
    }

    public void Save(DeploymentState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var pending = path + ".tmp";
        using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, state, JsonOptions);
            stream.Flush(true);
        }

        if (File.Exists(path))
        {
            try
            {
                if (JsonSerializer.Deserialize<DeploymentState>(File.ReadAllText(path)) != null)
                    File.Copy(path, path + ".bak", true);
            }
            catch (JsonException)
            {
            }
        }

        File.Move(pending, path, true);
    }
}

internal sealed record PackageRunResult(bool Success, bool Skipped = false, bool RestartRequired = false, string? Error = null);
internal static class InstallationRunner
{
    public static async Task RunAsync(DeploymentState state, IEnumerable<string> names, string bootId, Func<PackageCheckpoint, Task<PackageRunResult>> execute, Action save, Action changed)
    {
        state.RebootPending = false;
        state.BootId = bootId;
        foreach (var name in names)
        {
            var package = state.Packages.Single(p => p.Name == name);
            package.Status = PackageStatus.InProgress;
            package.Attempts++;
            package.Error = null;
            save();
            PackageRunResult result;
            try
            {
                result = await execute(package);
            }
            catch (Exception ex)
            {
                result = new(false, Error: ex.Message);
            }

            package.Status = result.RestartRequired && !result.Success ? PackageStatus.Pending : result.Success ? (result.Skipped ? PackageStatus.Skipped : PackageStatus.Succeeded) : PackageStatus.Failed;
            package.Error = result.Error;
            state.RebootPending |= result.RestartRequired;
            save();
            changed();
            if (state.RebootPending)
                break;
        }
    }
}
