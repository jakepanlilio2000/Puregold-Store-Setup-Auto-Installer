using System.IO;
using System.Text;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        public static string GlobalTempRoot { get; } = Path.Combine(Path.GetTempPath(), "PGInstaller_Session_" + Guid.NewGuid().ToString().Substring(0, 8));
        private async Task<bool> PrepareAssets()
        {
            string targetAssetsDir = @"C:\Assets";
            if (Directory.Exists(targetAssetsDir) && File.Exists(Path.Combine(targetAssetsDir, "chrome.exe")))
            {
                _assetsPath = targetAssetsDir;
                return true;
            }

            string zipFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets.zip");
            string tool7z = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");

            if (File.Exists(zipFile))
            {
                if (!Directory.Exists(targetAssetsDir))
                {
                    if (!File.Exists(tool7z))
                    {
                        Log("   [ERROR] 7z.exe missing.");
                        return false;
                    }

                    Log("   [INIT] Extracting Assets to C:\\Assets...");
                    Directory.CreateDirectory(targetAssetsDir);

                    string pw = Encoding.UTF8.GetString(Convert.FromBase64String("cHdAMTIzNA==")); 

                    await RunProcessAsync(tool7z, $"x \"{zipFile}\" -o\"{targetAssetsDir}\" -p{pw} -y", "Extracting Assets", true);
                }

                
                string sub = Path.Combine(targetAssetsDir, "assets");
                _assetsPath = Directory.Exists(sub) ? sub : targetAssetsDir;

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
