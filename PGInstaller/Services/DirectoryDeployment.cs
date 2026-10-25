using System.IO;

namespace PGInstaller.Services;
internal static class DirectoryDeployment
{
    public static async Task ReplaceAsync(string target, string replacement, string backup, Func<Task> activate, Func<Task> deactivate)
    {
        if (!Directory.Exists(target) || !Directory.Exists(replacement))
            throw new DirectoryNotFoundException("Both the existing installation and replacement must exist.");
        if (Directory.Exists(backup))
            throw new IOException("The backup directory already exists.");
        Directory.Move(target, backup);
        try
        {
            Directory.Move(replacement, target);
            await activate();
        }
        catch
        {
            await deactivate();
            if (Directory.Exists(target))
                Directory.Move(target, replacement);
            Directory.Move(backup, target);
            throw;
        }
    }
}
