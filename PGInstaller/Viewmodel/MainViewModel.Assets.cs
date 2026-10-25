using System.IO;
using PGInstaller.Services;

namespace PGInstaller.Viewmodel;
partial class MainViewModel
{
    public static string GlobalTempRoot { get; private set; } = Path.Combine(Path.GetTempPath(), $"PGInstaller_Session_{Guid.NewGuid():N}");

    private AssetStore? _assetStore;
    private AssetStore Assets => _assetStore ??= new AssetStore(AppContext.BaseDirectory, @"C:\Assets", Log);

    private static readonly HashSet<string> SessionRoots = [GlobalTempRoot];
    private bool HasMatchingAssetCatalog()
    {
        if (Assets.Manifest?.ReleaseVersion == ReleaseInfo.Version) return true;
        Log("   [ERROR] Copy the matching assets.manifest.json and archive beside Store Setup before using asset-based tools.");
        return false;
    }

    private Task<bool> ExtractSpecificFile(string? zipPath, string fileNamePattern, string? targetDir = null)
        => HasMatchingAssetCatalog() ? Assets.ExtractAsync(fileNamePattern, zipPath, targetDir) : Task.FromResult(false);
    private string? ResolveAssetPath(string fileName) => HasMatchingAssetCatalog() ? Assets.Resolve(fileName) : null;
    private Task<string?> EnsureAssetAsync(string fileName)
        => HasMatchingAssetCatalog() ? Assets.EnsureAsync(fileName) : Task.FromResult<string?>(null);

    private static void StartNewStagingSession()
    {
        GlobalTempRoot = Path.Combine(Path.GetTempPath(), $"PGInstaller_Session_{Guid.NewGuid():N}");
        SessionRoots.Add(GlobalTempRoot);
    }

    public void CleanupSession()
    {
        _checkInstalledCts?.Cancel();
        foreach (var root in SessionRoots)
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
