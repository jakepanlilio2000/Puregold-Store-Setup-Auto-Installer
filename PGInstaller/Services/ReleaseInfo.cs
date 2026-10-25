namespace PGInstaller.Services;
internal static class ReleaseInfo
{
    public static string Version => typeof(ReleaseInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
