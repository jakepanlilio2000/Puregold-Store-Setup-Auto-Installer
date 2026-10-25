using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PGInstaller.Services;
internal sealed record AssetFile(string Path, long Size, string Sha256, string Version);
internal sealed class AssetManifest
{
    public string ReleaseVersion { get; set; } = "";
    public List<AssetFile> Files { get; set; } = [];
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ReleaseVersion + string.Join("\n", Files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(f => $"{f.Path.ToLowerInvariant()}|{f.Size}|{f.Sha256.ToUpperInvariant()}|{f.Version}")))));

    public static AssetManifest Load(string path)
    {
        var manifest = JsonSerializer.Deserialize<AssetManifest>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("The asset catalog is empty.");
        if (string.IsNullOrWhiteSpace(manifest.ReleaseVersion) || manifest.Files.Count == 0)
            throw new InvalidDataException("The asset catalog has no release version or files.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            AssetStore.SafeDestination(System.IO.Path.GetTempPath(), file.Path);
            if (!names.Add(file.Path.Replace('\\', '/')) || file.Size < 0 || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException($"Invalid or duplicate catalog entry: {file.Path}");
        }

        return manifest;
    }

    public AssetFile? Find(string name) => Files.FirstOrDefault(f => AssetStore.Matches(f.Path, name));
}
