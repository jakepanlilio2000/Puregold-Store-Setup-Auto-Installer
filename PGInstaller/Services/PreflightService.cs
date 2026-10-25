namespace PGInstaller.Services;
public sealed record PreflightItem(string Name, string Status, string Details, bool IsBlocking = false, string? Package = null);
internal sealed record PreflightReport(List<PreflightItem> Items, List<string> ReadyPackages)
{
    public bool HasSystemBlocker => Items.Any(i => i.IsBlocking && i.Package == null);
    public bool CanProceed => !HasSystemBlocker && ReadyPackages.Count > 0;
}

internal static class PreflightService
{
    internal static readonly string[][] ExclusiveGroups = [["Wamp 1.7.2", "Wamp 2", "Wamp 2.5", "Wampserver 3.4.0"], ["Bartender 10.1", "Bartender 2016", "Bartender 2022"], ["Coreldraw Graphics X5", "Coreldraw Graphics X7"], ["Argox Driver", "Zebra Driver"]];
    public static long EstimateRequiredBytes(IEnumerable<AssetFile> files) => checked(files.DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Sum(f => f.Size * 4) + 1024L * 1024 * 1024);
    public static async Task<PreflightReport> CheckAsync(IEnumerable<string> selection, string? department, AssetStore assets, bool administrator, bool compatibleWindows, long freeBytes, bool installerActive, IEnumerable<string> installed, IEnumerable<string>? configurationAssets = null, bool allowEmpty = false)
    {
        var selected = selection.Distinct().ToList();
        var items = new List<PreflightItem>
        {
            new("Administrator access", administrator ? "Ready" : "Blocked", administrator ? "Elevated access is available." : "Run Store Setup as administrator.", !administrator),
            new("Windows compatibility", compatibleWindows ? "Ready" : "Blocked", "Requires 64-bit Windows 10 or later.", !compatibleWindows),
            new("Windows Installer", installerActive ? "Blocked" : "Ready", installerActive ? "Another MSI installation is in progress. Finish it and check again." : "No active MSI transaction detected.", installerActive)
        };
        if (selected.Count == 0 && !allowEmpty)
            items.Add(new("Applications", "Blocked", "Select at least one application.", true));
        if (assets.Manifest == null)
        {
            items.Add(new("Asset catalog", "Blocked", "assets.manifest.json is missing or invalid. Rebuild or copy the complete release.", true));
            return new(items, []);
        }

        items.Add(new("Asset catalog", "Ready", $"Release {assets.Manifest.ReleaseVersion}; SHA-256 verification enabled."));
        if (assets.Manifest.ReleaseVersion != ReleaseInfo.Version)
        {
            items.Add(new("Release version", "Blocked", $"Catalog {assets.Manifest.ReleaseVersion} does not match application {ReleaseInfo.Version}. Copy the complete matching release.", true));
            return new(items, []);
        }

        var files = new List<AssetFile>();
        var ready = new List<string>();
        var conflicts = ExclusiveGroups.Where(g => g.Count(selected.Contains) > 1).SelectMany(g => g.Where(selected.Contains)).ToHashSet();
        foreach (var app in selected)
        {
            if (conflicts.Contains(app))
            {
                items.Add(new(app, "Conflict", "Select only one version or driver in this group.", true, app));
                continue;
            }

            if (!PackageAssets.Required.ContainsKey(app))
            {
                items.Add(new(app, "Blocked", "No package definition is available.", true, app));
                continue;
            }

            var missing = new List<string>();
            var appFiles = new List<AssetFile>();
            foreach (var alternatives in PackageAssets.For(app, department))
            {
                AssetFile? match = null;
                foreach (var name in alternatives)
                {
                    var entry = assets.Manifest.Find(name);
                    if (entry != null && await assets.CanSupplyAsync(entry))
                    {
                        match = entry;
                        break;
                    }
                }

                if (match == null)
                    missing.Add(string.Join(" or ", alternatives));
                else
                {
                    files.Add(match);
                    appFiles.Add(match);
                }
            }

            if (missing.Count > 0)
                items.Add(new(app, "Missing", "Missing or unverified: " + string.Join(", ", missing), true, app));
            else
            {
                ready.Add(app);
                var other = ExclusiveGroups.FirstOrDefault(g => g.Contains(app))?.Where(n => n != app && installed.Contains(n)).ToArray() ?? [];
                items.Add(new(app, other.Length == 0 ? "Ready" : "Warning", other.Length == 0 ? appFiles.Count == 0 ? "Uses the script bundled with Store Setup." : "Available: " + string.Join("; ", appFiles.Select(f => $"{System.IO.Path.GetFileName(f.Path)} · {f.Version}")) + ". Cached hashes checked; archive files are verified before installation." : "Existing version detected: " + string.Join(", ", other) + ". This installation may replace it.", false, app));
            }
        }

        foreach (var name in configurationAssets ?? [])
        {
            var entry = assets.Manifest.Find(name);
            if (entry == null || !await assets.CanSupplyAsync(entry))
                items.Add(new(name, "Blocked", "A selected Windows setting requires this asset.", true));
            else
                files.Add(entry);
        }

        long required = EstimateRequiredBytes(files);
        items.Add(new("Disk space", freeBytes >= required ? "Ready" : "Blocked", $"{freeBytes / 1073741824.0:F1} GiB free; estimated {required / 1073741824.0:F1} GiB required for caching, staging, and installation. Package requirements may vary.", freeBytes < required));
        return new(items, ready);
    }
}
