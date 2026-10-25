using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PGInstaller.Services;
internal sealed class AssetStore(string applicationDirectory, string cacheDirectory, Action<string> log)
{
    private readonly SemaphoreSlim _extractionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, (long Size, long Modified, string Hash)> _verified = new(StringComparer.OrdinalIgnoreCase);
    private const string Password = "pw@1234";
    private string ArchivePath => Path.Combine(applicationDirectory, "assets.zip");
    public AssetManifest? Manifest { get; } = ReadManifest(applicationDirectory, log);
    public string? CatalogFingerprint => Manifest?.Fingerprint;
    private IEnumerable<string> Roots => new[]
    {
        Path.Combine(cacheDirectory, "Packages"),
        Path.Combine(applicationDirectory, "assets"),
        Path.Combine(cacheDirectory, "assets"),
        cacheDirectory
    }.Distinct(StringComparer.OrdinalIgnoreCase);
    public bool IsAvailable => File.Exists(ArchivePath) || Roots.Any(Directory.Exists);

    public void ResetVerification() => _verified.Clear();
    private static AssetManifest? ReadManifest(string directory, Action<string> log)
    {
        var path = Path.Combine(directory, "assets.manifest.json");
        if (!File.Exists(path))
            return null;
        try
        {
            return AssetManifest.Load(path);
        }
        catch (Exception ex)
        {
            log($"   [ERROR] Invalid asset catalog: {ex.Message}");
            return null;
        }
    }

    public string? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        if (Path.IsPathRooted(name))
            return File.Exists(name) ? name : null;
        var expected = Manifest?.Find(name);
        if (Manifest != null && expected == null && !name.Contains('\\') && !name.Contains('/'))
            return null;
        foreach (var root in Roots.Where(Directory.Exists))
        {
            var direct = SafeDestination(root, name);
            if (IsValid(direct, expected))
                return direct;
            try
            {
                foreach (var path in Directory.EnumerateFiles(root, Path.GetFileName(name), new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                    if (IsValid(path, expected))
                        return path;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    private bool IsValid(string path, AssetFile? expected)
    {
        if (!File.Exists(path))
            return false;
        if (expected == null)
            return true;
        try
        {
            var info = new FileInfo(path);
            if (info.Length != expected.Size)
                return false;
            if (_verified.TryGetValue(path, out var cached) && cached.Size == info.Length && cached.Modified == info.LastWriteTimeUtc.Ticks)
                return cached.Hash.Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase);
            using var stream = File.OpenRead(path);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            _verified[path] = (info.Length, info.LastWriteTimeUtc.Ticks, hash);
            var valid = hash.Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase);
            if (!valid)
                log($"   [VERIFY] Outdated or corrupt asset will be replaced: {path}");
            return valid;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task<bool> CanSupplyAsync(AssetFile file)
    {
        if (await Task.Run(() => Resolve(file.Path)) != null)
            return true;
        if (!File.Exists(ArchivePath))
            return false;
        try
        {
            return await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(ArchivePath);
                return zip.Entries.Any(e => e.Length == file.Size && Matches(e.FullName, file.Path));
            });
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public async Task<string?> EnsureAsync(string name)
    {
        var existing = await Task.Run(() => Resolve(name));
        if (existing != null)
            return existing;
        return await ExtractAsync(name) ? await Task.Run(() => Resolve(name)) : null;
    }

    public async Task<bool> ExtractAsync(string pattern, string? archivePath = null, string? destination = null)
    {
        await _extractionLock.WaitAsync();
        try
        {
            var archive = archivePath ?? ArchivePath;
            var target = destination ?? Path.Combine(cacheDirectory, "Packages");
            if (!File.Exists(archive))
            {
                log($"   [ERROR] Asset archive not found: {archive}");
                return false;
            }

            var tool = Path.Combine(applicationDirectory, "7z.exe");
            if (!File.Exists(tool))
                throw new FileNotFoundException("7z.exe is required for encrypted assets.", tool);
            var entries = await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(archive);
                return zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name) && Matches(e.FullName, pattern)).Select(e => e.FullName).ToArray();
            });
            if (entries.Length == 0)
            {
                log($"   [MISSING] '{pattern}' is not in {Path.GetFileName(archive)}.");
                return false;
            }

            foreach (var entry in entries)
            {
                SafeDestination(target, entry);
                if (Manifest != null && Manifest.Find(entry.Replace('\\', '/').StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ? entry[7..] : entry) == null)
                    throw new InvalidDataException($"Archive member is absent from the catalog: {entry}");
            }

            AssetFile? Expected(string entry) => Manifest?.Files.FirstOrDefault(f => Matches(entry, f.Path));
            if (await Task.Run(() => entries.All(e => IsValid(SafeDestination(target, e), Expected(e)))))
                return true;
            Directory.CreateDirectory(target);
            var list = Path.Combine(Path.GetTempPath(), $"pg-assets-{Guid.NewGuid():N}.txt");
            try
            {
                await File.WriteAllLinesAsync(list, entries, new UTF8Encoding(false));
                log($"   [EXTRACT] {pattern} ({entries.Length} file(s))");
                var start = new ProcessStartInfo(tool)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var argument in new[]
                {
                    "x",
                    archive,
                    $"-o{target}",
                    $"-p{Password}",
                    "-y",
                    "-aoa",
                    "-spd",
                    "-scsUTF-8",
                    $"-i@{list}"}

                )
                    start.ArgumentList.Add(argument);
                using var process = Process.Start(start) ?? throw new IOException("Cannot start 7-Zip.");
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(process.WaitForExitAsync(), output, error);
                _verified.Clear();
                if (process.ExitCode != 0 || !await Task.Run(() => entries.All(e => IsValid(SafeDestination(target, e), Expected(e)))))
                {
                    foreach (var entry in entries)
                    {
                        var file = SafeDestination(target, entry);
                        if (File.Exists(file))
                            File.Delete(file);
                    }

                    log($"   [ERROR] Extraction or SHA-256 verification failed for {pattern} (exit {process.ExitCode}). {(await error).Trim()}");
                    return false;
                }

                return true;
            }
            finally
            {
                File.Delete(list);
            }
        }
        catch (Exception ex)
        {
            log($"   [ERROR] Cannot extract '{pattern}': {ex.Message}");
            return false;
        }
        finally
        {
            _extractionLock.Release();
        }
    }

    internal static bool Matches(string entry, string pattern)
    {
        var normalized = entry.Replace('\\', '/');
        var requested = pattern.Replace('\\', '/');
        var candidate = requested.Contains('/') ? normalized : normalized.Split('/')[^1];
        var expression = "^" + Regex.Escape(requested).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(candidate, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || (requested.Contains('/') && normalized.EndsWith("/" + requested, StringComparison.OrdinalIgnoreCase));
    }

    internal static string SafeDestination(string root, string entry)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(fullRoot, entry.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || entry.Contains(':') || entry.Contains('\n') || entry.Contains('\r'))
            throw new InvalidDataException($"Archive entry escapes the destination: {entry}");
        return path;
    }
}
