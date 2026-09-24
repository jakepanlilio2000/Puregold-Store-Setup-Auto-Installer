using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        public static string GlobalTempRoot { get; } = Path.Combine(Path.GetTempPath(), string.Concat("PGInstaller_Session_", Guid.NewGuid().ToString().AsSpan(0, 8)));

        /// <summary>
        /// Prepares the complete assets folder by extracting assets.zip if not already present.
        /// Used for full installation sequences.
        /// </summary>
        /// <returns>True if assets are ready, false otherwise.</returns>
        private async Task<bool> PrepareAssets()
        {
            string targetAssetsDir = @"C:\Assets";
            string sub = Path.Combine(targetAssetsDir, "assets");

            bool hasFiles = (Directory.Exists(targetAssetsDir) && (File.Exists(Path.Combine(targetAssetsDir, "chrome.exe")) || Directory.GetFiles(targetAssetsDir).Length > 3)) ||
                            (Directory.Exists(sub) && (File.Exists(Path.Combine(sub, "chrome.exe")) || Directory.GetFiles(sub).Length > 3));

            if (hasFiles)
            {
                _assetsPath = (Directory.Exists(sub) && File.Exists(Path.Combine(sub, "chrome.exe"))) ? sub : targetAssetsDir;
                return true;
            }

            string zipFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets.zip");
            string tool7z = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");

            if (File.Exists(zipFile))
            {
                if (!File.Exists(tool7z))
                {
                    Log("   [ERROR] 7z.exe missing for assets extraction.");
                    return false;
                }

                Log("   [INIT] Extracting Assets to C:\\Assets...");
                if (!Directory.Exists(targetAssetsDir))
                {
                    Directory.CreateDirectory(targetAssetsDir);
                }

                string pw = Encoding.UTF8.GetString(Convert.FromBase64String("cHdAMTIzNA=="));
                await RunProcessAsync(tool7z, $"x \"{zipFile}\" -o\"{targetAssetsDir}\" -p{pw} -y", "Extracting Assets", true);

                _assetsPath = Directory.Exists(sub) ? sub : targetAssetsDir;
                return true;
            }

            if (Directory.Exists(targetAssetsDir))
            {
                _assetsPath = Directory.Exists(sub) ? sub : targetAssetsDir;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts only matching files from assets.zip on demand into C:\Assets without extracting the entire archive.
        /// </summary>
        /// <param name="zipPath">Path to the assets archive (or uses default assets.zip if omitted or empty).</param>
        /// <param name="fileNamePattern">File name or wildcard pattern to extract (e.g. "*MAS_AIO*", "*AchillesScript*").</param>
        /// <param name="targetDir">Target directory (defaults to C:\Assets).</param>
        /// <returns>True if extraction succeeded or matching file already exists.</returns>
        private async Task<bool> ExtractSpecificFile(string? zipPath, string fileNamePattern, string? targetDir = null)
        {
            string destDir = targetDir ?? @"C:\Assets";
            _assetsPath ??= destDir;

            string? existing = ResolveAssetPath(fileNamePattern);
            if (!string.IsNullOrEmpty(existing) && (File.Exists(existing) || Directory.Exists(existing)))
            {
                return true;
            }

            string zipFile = (!string.IsNullOrEmpty(zipPath) && File.Exists(zipPath))
                ? zipPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets.zip");

            if (!File.Exists(zipFile))
            {
                Log($"   [ERROR] Assets archive not found: {zipFile}");
                return false;
            }

            string tool7z = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");
            if (!File.Exists(tool7z))
            {
                Log("   [ERROR] 7z.exe missing for on-demand extraction.");
                return false;
            }

            Directory.CreateDirectory(destDir);
            string pw = Encoding.UTF8.GetString(Convert.FromBase64String("cHdAMTIzNA=="));

            Log($"   [EXTRACT] On-demand extraction of '{fileNamePattern}' from assets.zip...");
            bool success = await RunProcessAsync(
                tool7z,
                $"x \"{zipFile}\" -o\"{destDir}\" \"{fileNamePattern}\" -p{pw} -y -r",
                $"Extracting {fileNamePattern}",
                true
            );

            string sub = Path.Combine(destDir, "assets");
            if (Directory.Exists(sub)) _assetsPath = sub;

            return success;
        }

        /// <summary>
        /// Cleans up temporary installation staging directories.
        /// </summary>
        public void CleanupSession()
        {
            string[] cleanupDirs = [
                @"C:\PG_Activator",
                @"C:\Assets\AV_Install",
                @"C:\Assets\Bartender_Install",
                @"C:\Assets\PG_CBM_Exec",
                @"C:\Assets\PG_FSDM_Install",
                @"C:\Assets\PG_PIMS_Install",
                @"C:\Assets\NetFX3_Source"
            ];

            foreach (var dir in cleanupDirs)
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch { }
                }
            }
        }
    }
}
