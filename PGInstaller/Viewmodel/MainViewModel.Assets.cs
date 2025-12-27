using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private async Task<bool> PrepareAssets()
        {
            string localAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            string zipFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets.zip");
            string tempRoot = Path.Combine(Path.GetTempPath(), "PGInstaller_Assets");
            string tool7z = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");

            if (File.Exists(Path.Combine(localAssets, "chrome.exe")))
            {
                _assetsPath = localAssets;
                Log($"   [INIT] Using Local Assets folder.");
                return true;
            }

            if (File.Exists(zipFile))
            {
                string securePw = Encoding.UTF8.GetString(Convert.FromBase64String("cHdAMTIzNA=="));

                if (!File.Exists(tool7z))
                {
                    Log(
                        "   [ERROR] 7z.exe not found next to application. Cannot decrypt assets.zip."
                    );
                    return false;
                }
                if (Directory.Exists(tempRoot))
                {
                    try
                    {
                        Directory.Delete(tempRoot, true);
                    }
                    catch
                    {
                        Log("   [WARN] Could not clean temp folder. Trying to overwrite.");
                    }
                }

                Log("   [INFO] Decrypting and Extracting assets.zip...");
                Directory.CreateDirectory(tempRoot);
                string args = $"x \"{zipFile}\" -o\"{tempRoot}\" -p{securePw} -y";

                bool success = await RunProcessAsync(tool7z, args, "Extracting Assets", true);
                if (!success)
                    return false;
                string subFolder = Path.Combine(tempRoot, "assets");
                _assetsPath = Directory.Exists(subFolder) ? subFolder : tempRoot;
                Log($"   [INIT] Assets ready at: {_assetsPath}");
                return true;
            }

            Log("   [WARNING] No Assets folder or assets.zip found.");
            return false;
        }


    }
}
