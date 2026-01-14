using System.IO;
using System.Text;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        public static string GlobalTempRoot { get; } = Path.Combine(Path.GetTempPath(), "PGInstaller_Session_" + Guid.NewGuid().ToString().Substring(0, 8));
        private async Task<bool> PrepareAssets()
        {
            string zipFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets.zip");
            string tool7z = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");

            if (Directory.Exists(_assetsPath) && File.Exists(Path.Combine(_assetsPath, "chrome.exe")))
            {
                return true;
            }
            if (File.Exists(zipFile))
            {
                string tempAssetsDir = Path.Combine(GlobalTempRoot, "Assets");

                if (!Directory.Exists(tempAssetsDir))
                {
                    if (!File.Exists(tool7z)) { Log("   [ERROR] 7z.exe missing."); return false; }

                    Log("   [INIT] Extracting Assets (This stays until app closes)...");
                    Directory.CreateDirectory(tempAssetsDir);

                    string pw = Encoding.UTF8.GetString(Convert.FromBase64String("cHdAMTIzNA=="));
                    await RunProcessAsync(tool7z, $"x \"{zipFile}\" -o\"{tempAssetsDir}\" -p{pw} -y", "Extracting Assets", true);
                }
                string sub = Path.Combine(tempAssetsDir, "assets");
                _assetsPath = Directory.Exists(sub) ? sub : tempAssetsDir;
                return true;
            }

            return false;
        }

        public void CleanupSession()
        {
            if (Directory.Exists(GlobalTempRoot))
            {
                try
                {
                    Directory.Delete(GlobalTempRoot, true);
                }
                catch { }
            }
        }


    }
}
