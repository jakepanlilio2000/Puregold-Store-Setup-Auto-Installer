using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PGInstaller;
using PGInstaller.Services;
using PGInstaller.Viewmodel;

internal static class Program
{
    private static int _passed;
    private static string _root = "";

    [STAThread]
    private static int Main(string[] args)
    {
        _root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        try
        {
            if (args.Contains("--clean")) { RemoveComments(); return 0; }
            if (args.Contains("--audit")) { AuditReleaseAsync().GetAwaiter().GetResult(); return 0; }
            CheckAssetsAsync().GetAwaiter().GetResult();
            CheckDeploymentAsync().GetAwaiter().GetResult();
            CheckCatalogAndPreflightAsync().GetAwaiter().GetResult();
            CheckJournalAndResumeAsync().GetAwaiter().GetResult();
            CheckUi();
            Console.WriteLine($"PASS: {_passed} checks");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        _passed++;
        Console.WriteLine($"PASS: {name}");
    }

    private static async Task CheckAssetsAsync()
    {
        var fixture = Path.Combine(_root, "artifacts", "checks", Guid.NewGuid().ToString("N"));
        var app = Path.Combine(fixture, "app");
        var cache = Path.Combine(fixture, "cache");
        var source = Path.Combine(fixture, "assets");
        Directory.CreateDirectory(Path.Combine(source, "av"));
        Directory.CreateDirectory(app);
        foreach (var name in new[] { "7z.exe", "7z.dll" }) File.Copy(Path.Combine(_root, "PGInstaller", name), Path.Combine(app, name));
        await File.WriteAllTextAsync(Path.Combine(source, "chrome.exe"), "selected package");
        await File.WriteAllTextAsync(Path.Combine(source, "Firefox.exe"), "unselected package");
        await File.WriteAllTextAsync(Path.Combine(source, "av", "tool.exe"), "nested package");
        await File.WriteAllTextAsync(Path.Combine(source, "A & space.txt"), "special characters");
        var archive = Path.Combine(app, "assets.zip");
        var start = new ProcessStartInfo(Path.Combine(app, "7z.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = fixture,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { "a", "-tzip", archive, "assets", "-ppw@1234", "-mem=AES256", "-bso0", "-bsp0" }) start.ArgumentList.Add(arg);
        using (var process = Process.Start(start)!)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(process.WaitForExitAsync(), output, errors);
            Check(process.ExitCode == 0, "encrypted fixture created");
        }
        var store = new AssetStore(app, cache, Console.WriteLine);
        Check(store.IsAvailable, "archive works without an existing cache");
        Check(await store.ExtractAsync("chrome.exe"), "selected file extracted from encrypted wrapped archive");
        Check(store.Resolve("chrome.exe") is { } chrome && File.ReadAllText(chrome) == "selected package", "extracted bytes match");
        Check(Directory.GetFiles(cache, "*", SearchOption.AllDirectories).Length == 1, "unselected packages remain compressed");
        File.Delete(store.Resolve("chrome.exe")!);
        Check(await store.ExtractAsync("chrome.exe") && store.Resolve("chrome.exe") != null, "missing cached package is restored on demand");
        Check(!await store.ExtractAsync("does-not-exist.exe"), "zero matching archive members fails");
        Check(!await store.ExtractAsync("*chrome.exe.bak*"), "similar names do not match exact package names");
        Check(await store.ExtractAsync("av/tool.exe"), "nested member paths are supported");
        Check(await store.ExtractAsync("A & space.txt"), "spaces and ampersands are preserved");
        Check((await Task.WhenAll(store.ExtractAsync("Firefox.exe"), store.ExtractAsync("chrome.exe"))).All(v => v), "concurrent requests serialize extraction");
        var explicitTarget = Path.Combine(fixture, "explicit");
        Check(await store.ExtractAsync("chrome.exe", archive, explicitTarget) && File.Exists(Path.Combine(explicitTarget, "assets", "chrome.exe")), "explicit extraction target is honored");
        var flat = Path.Combine(fixture, "flat.zip");
        using (var zip = ZipFile.Open(flat, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("flat.txt").Open());
            writer.Write("flat archive");
        }
        Check(await store.ExtractAsync("flat.txt", flat), "flat archive layout is supported");
        foreach (var entry in new[] { "../cache-sibling/escape.txt", "..\\escape.txt", @"C:\escape.txt", "file.txt:stream" })
        {
            bool rejected = false;
            try { AssetStore.SafeDestination(cache, entry); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, $"unsafe entry rejected: {entry}");
        }
        Check(AssetStore.Matches("assets/av/tool.exe", "av/tool.exe"), "wrapped paths match relative requests");
        Check(!AssetStore.Matches("assets/chrome.exe.backup", "chrome.exe"), "exact matching excludes backup files");
        var malicious = Path.Combine(fixture, "malicious.zip");
        using (var zip = ZipFile.Open(malicious, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("../escape.txt").Open()); writer.Write("escape");
        }
        Check(!await store.ExtractAsync("escape.txt", malicious), "archive traversal is rejected before extraction");
        Check(!await store.ExtractAsync("chrome.exe", Path.Combine(fixture, "missing.zip")), "missing archive fails");
    }

    private static void CheckUi()
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        var vm = (MainViewModel)window.DataContext;
        typeof(MainViewModel).GetField("_packageRestartRequired", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, true);
        var hrdTask = (Task)typeof(MainViewModel).GetMethod("DeployHrdWampContent", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null)!;
        bool hrdRestartGuard = false;
        try { hrdTask.GetAwaiter().GetResult(); } catch (InvalidOperationException) { hrdRestartGuard = true; }
        Check(hrdRestartGuard, "HRD content cannot be replaced while an installer restart is pending");
        typeof(MainViewModel).GetField("_packageRestartRequired", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, false);
        Check(!vm.DisableFirewall && !vm.SetWallpaper && !vm.SetPowerOptions && !vm.ConfigureTaskbar && !vm.OptimizeWindows, "Windows changes are opt-in and the firewall stays enabled by default");
        vm.PreflightItems.Add(new("Administrator access", "Ready", "Administrator access is available."));
        vm.PreflightItems.Add(new("Oracle Java Runtime", "Missing", "jre.exe or java.exe is missing.", true, "Oracle Java Runtime"));
        vm.DeploymentResults.Add(new("Google Chrome", "Succeeded", "Attempts: 1."));
        vm.DeploymentResults.Add(new("Oracle Java Runtime", "Blocked", "Add the Java installer and retry.", true, "Oracle Java Runtime"));
        var uiJournal = new InstallJournal(Path.Combine(_root, "artifacts", "checks", "ui-" + Guid.NewGuid().ToString("N"), "state.json"));
        var uiState = new DeploymentState
        {
            BootId = "ui-boot", Packages = [new() { Name = "Google Chrome", Status = PackageStatus.Succeeded, Attempts = 1 }, new() { Name = "Oracle Java Runtime", Status = PackageStatus.Blocked, Error = "Add the Java installer and retry." }, new() { Name = "WinRAR", Status = PackageStatus.Pending }]
        };
        uiJournal.Save(uiState);
        typeof(MainViewModel).GetField("_journal", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, uiJournal);
        typeof(MainViewModel).GetField("_bootId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, "ui-boot");
        typeof(MainViewModel).GetMethod("RefreshSavedDeployment", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null);
        Check(vm.ResumeDeploymentCommand.CanExecute(null) && vm.RetryPackageCommand.CanExecute("Oracle Java Runtime") && !vm.RetryPackageCommand.CanExecute("Google Chrome"), "saved state enables resume and retry only for eligible packages");
        var runner = typeof(MainViewModel).GetMethod("RunProcessAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var code in new[] { 0, 3010, 1641, 1603 })
        {
            var task = (Task<bool>)runner.Invoke(vm, ["powershell.exe", $"-NoProfile -Command \"exit {code}\"", "Process result check", true])!;
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => app.Dispatcher.BeginInvoke(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Check(task.GetAwaiter().GetResult() == (code is 0 or 3010 or 1641), $"process exit {code} is classified correctly");
        }
        var shell = typeof(MainViewModel).GetMethod("RunPowerShellAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var shellTask = (Task<bool>)shell.Invoke(vm, ["Write-Output \"A & 'quoted' value\"; [Console]::Error.WriteLine('stderr drained'); exit 0", "PowerShell argument check", false])!;
        var shellFrame = new DispatcherFrame();
        shellTask.ContinueWith(_ => app.Dispatcher.BeginInvoke(() => shellFrame.Continue = false));
        Dispatcher.PushFrame(shellFrame);
        Check(shellTask.GetAwaiter().GetResult() && vm.LogOutput!.Contains("A & 'quoted' value") && vm.LogOutput.Contains("stderr drained"), "encoded PowerShell preserves quotes and drains both streams");
        vm.SelectedDepartment = "HRD";
        Check(vm.PreviewList.Single(x => x.Name == "Wamp 2").IsChecked, "HRD defaults to Wamp 2");
        Check(!vm.PreviewList.Single(x => x.Name == "Wamp 1.7.2").IsChecked, "HRD no longer defaults to Wamp 1.7.2");
        Check(vm.PreviewList.Select(x => x.Name).Distinct().Count() == vm.PreviewList.Count, "manifest has no duplicate apps");
        Check(vm.PreviewList.All(x => PackageAssets.Required.ContainsKey(x.Name)), "every selectable app has an asset manifest");
        Check(PackageAssets.For("Wamp 2", "HRD").Any(x => x.Contains("wampHR.zip")), "HRD manifest includes replacement content");
        Check(!PackageAssets.For("Wamp 2", "IT").Any(x => x.Contains("wampHR.zip")), "HRD replacement stays scoped to HRD");
        foreach (var item in vm.PreviewList) item.IsInstalled = false;
        vm.ManifestSearchText = "Firefox";
        Check(vm.FilteredPreviewList.Cast<InstallAppItem>().Single().Name == "Mozilla Firefox", "manifest search filters applications");
        vm.ManifestSearchText = "";
        vm.PreviewList.Single(x => x.Name == "Wamp 2.5").IsChecked = true;
        Check(!vm.PreviewList.Single(x => x.Name == "Wamp 2").IsChecked, "Wamp choices are mutually exclusive");
        var installed = vm.PreviewList.First();
        installed.IsInstalled = true;
        installed.ForceInstall = true;
        vm.DeselectAllManifestCommand.Execute(null);
        Check(!installed.ForceInstall, "clear selection also clears reinstall flags");
        vm.IsBusy = true;
        Check(!vm.IsIdle && !vm.InstallCommand.CanExecute(null), "busy state prevents competing installations");
        vm.IsBusy = false;
        vm.SelectedDepartment = "IT";
        vm.SelectedDepartment = "HRD";
        vm.PcName = "PG-HRD-01"; vm.CpuInfo = "Intel Core i5 · Workstation"; vm.RamInfo = "16 GB RAM";
        vm.OsVersion = "Windows 11 Pro · 24H2"; vm.DomainStatus = "Joined: puregold.local";
        var failures = new StringWriter();
        PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(failures));
        var output = Path.Combine(_root, "artifacts", "previews");
        Directory.CreateDirectory(output);
        var content = (FrameworkElement)window.Content;
        Render(content, 1280, 810, Path.Combine(output, "applications.png"));
        Render(content, 1050, 640, Path.Combine(output, "applications-compact.png"));
        var tabs = Descendants<TabControl>(content).Single();
        for (int i = 1; i < tabs.Items.Count; i++)
        {
            tabs.SelectedIndex = i;
            Render(content, 1280, 810, Path.Combine(output, $"tab-{i}.png"));
        }
        tabs.SelectedIndex = 0;
        Descendants<Expander>(content).Single().IsExpanded = true;
        Render(content, 1280, 810, Path.Combine(output, "windows-settings.png"));
        var login = new LoginWindow();
        Render((FrameworkElement)login.Content, 420, 430, Path.Combine(output, "login.png"));
        Check(!failures.ToString().Contains("Error:"), "rendered XAML bindings have no errors");
        vm.CleanupSession();
        Console.WriteLine($"UI previews: {output}");
    }

    private static void Render(FrameworkElement content, int width, int height, string path)
    {
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
        Check(content.ActualWidth == width, $"rendered {Path.GetFileName(path)}");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void RemoveComments()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(_root, "PGInstaller"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains("\\obj\\") && !p.Contains("\\bin\\")))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            var root = tree.GetRoot();
            var comments = root.DescendantTrivia(descendIntoTrivia: true).Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) ||
                t.IsKind(SyntaxKind.RegionDirectiveTrivia) || t.IsKind(SyntaxKind.EndRegionDirectiveTrivia));
            File.WriteAllText(file, root.ReplaceTrivia(comments, (_, _) => SyntaxFactory.Whitespace("")).NormalizeWhitespace(indentation: "    ", eol: "\n").ToFullString() + "\n");
        }
    }

    private static async Task CheckDeploymentAsync()
    {
        var fixture = Path.Combine(_root, "artifacts", "checks", Guid.NewGuid().ToString("N"));
        var target = Path.Combine(fixture, "wamp");
        var replacement = Path.Combine(fixture, "replacement");
        var backup = Path.Combine(fixture, "backup");
        Directory.CreateDirectory(target); Directory.CreateDirectory(replacement);
        await File.WriteAllTextAsync(Path.Combine(target, "old.txt"), "original");
        await File.WriteAllTextAsync(Path.Combine(replacement, "new.txt"), "HRD");
        await DirectoryDeployment.ReplaceAsync(target, replacement, backup, () => Task.CompletedTask, () => Task.CompletedTask);
        Check(File.Exists(Path.Combine(target, "new.txt")) && !File.Exists(Path.Combine(target, "old.txt")), "HRD replacement removes obsolete original content");
        Check(File.ReadAllText(Path.Combine(backup, "old.txt")) == "original", "HRD original content is retained in a backup");
        Directory.CreateDirectory(replacement);
        await File.WriteAllTextAsync(Path.Combine(replacement, "bad.txt"), "bad content");
        var rollbackBackup = Path.Combine(fixture, "rollback-backup");
        bool deactivated = false;
        try
        {
            await DirectoryDeployment.ReplaceAsync(target, replacement, rollbackBackup,
                () => throw new IOException("Simulated service start failure"),
                () => { deactivated = true; return Task.CompletedTask; });
        }
        catch (IOException) { }
        Check(deactivated && File.ReadAllText(Path.Combine(target, "new.txt")) == "HRD" && !File.Exists(Path.Combine(target, "bad.txt")), "failed HRD activation rolls back the content");
    }

    private static async Task AuditReleaseAsync()
    {
        var release = Path.Combine(_root, "publish");
        var archive = Path.Combine(release, "assets.zip");
        using var zip = ZipFile.OpenRead(archive);
        var names = zip.Entries.Where(e => e.Name != "").Select(e => e.FullName).ToArray();
        var missing = new List<string>();
        foreach (var app in PackageAssets.Required.Keys)
            foreach (var group in PackageAssets.For(app, "HRD"))
                if (!group.Any(pattern => names.Any(entry => AssetStore.Matches(entry, pattern))))
                    missing.Add($"{app}: {string.Join(" or ", group)}");
        var report = $"Asset archive: {archive}\nFiles: {names.Length}\nMissing: {missing.Count}\n" + string.Join("\n", missing);
        Console.WriteLine(report);
        await File.WriteAllTextAsync(Path.Combine(_root, "artifacts", "asset-audit.txt"), report);
        var manifest = AssetManifest.Load(Path.Combine(release, "assets.manifest.json"));
        Check(names.Length == manifest.Files.Count, $"all {manifest.Files.Count} cataloged files are archived");
        Check(manifest.Files.All(f => zip.Entries.Any(e => AssetStore.Matches(e.FullName, f.Path) && e.Length == f.Size)), "archive paths and sizes match the release catalog");
        Check(manifest.ReleaseVersion == "4.1.0", "release catalog version is 4.1.0");
        Check(missing.Count == 1 && missing[0].StartsWith("Oracle Java Runtime:"), "only Java is absent from the supplied selectable packages");
        var cache = Path.Combine(_root, "artifacts", "checks", "real-archive-" + Guid.NewGuid().ToString("N"));
        var store = new AssetStore(release, cache, Console.WriteLine);
        Check(await store.ExtractAsync("AS400.KMP"), "release archive decrypts and extracts a selected file");
        Check(Directory.GetFiles(cache, "*", SearchOption.AllDirectories).Length == 1, "release extraction leaves all other packages compressed");
        Check(new FileInfo(store.Resolve("AS400.KMP")!).Length == 142, "real selected asset has the expected size");
    }

    private static async Task CheckCatalogAndPreflightAsync()
    {
        var fixture = Path.Combine(_root, "artifacts", "checks", "catalog-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(fixture, "app");
        var source = Path.Combine(app, "assets");
        var cache = Path.Combine(fixture, "cache");
        Directory.CreateDirectory(source);
        var original = "verified installer";
        await File.WriteAllTextAsync(Path.Combine(source, "chrome.exe"), original);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(original)));
        var catalog = new AssetManifest { ReleaseVersion = "4.1.0", Files = [new("chrome.exe", original.Length, hash, "1.2.3")] };
        var catalogPath = Path.Combine(app, "assets.manifest.json");
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(catalog));
        var store = new AssetStore(app, cache, Console.WriteLine);
        Check(store.Manifest?.Find("chrome.exe")?.Version == "1.2.3", "catalog carries asset versions");
        Check(store.Resolve("chrome.exe") != null, "matching asset hash is accepted");
        await File.WriteAllTextAsync(Path.Combine(source, "chrome.exe"), new string('x', original.Length));
        store.ResetVerification();
        Check(store.Resolve("chrome.exe") == null, "same-size corruption is rejected by SHA-256");
        await File.WriteAllTextAsync(Path.Combine(source, "chrome.exe"), original);
        store.ResetVerification();
        var count = Directory.GetFiles(fixture, "*", SearchOption.AllDirectories).Length;
        var report = await PreflightService.CheckAsync(["Google Chrome", "Oracle Java Runtime"], "HRD", store, true, true, long.MaxValue, false, []);
        Check(report.ReadyPackages.SequenceEqual(["Google Chrome"]) && report.Items.Any(i => i.Package == "Oracle Java Runtime" && i.IsBlocking), "preflight separates ready and unavailable packages");
        Check(Directory.GetFiles(fixture, "*", SearchOption.AllDirectories).Length == count && !Directory.Exists(cache), "preflight does not extract or change source files");
        report = await PreflightService.CheckAsync(["Google Chrome"], "IT", store, false, false, 0, true, []);
        Check(report.HasSystemBlocker && !report.CanProceed && report.Items.Count(i => i.IsBlocking && i.Package == null) == 4, "preflight blocks missing elevation, unsupported Windows, active MSI, and insufficient disk space");
        report = await PreflightService.CheckAsync(["Wamp 2", "Wamp 2.5"], "HRD", store, true, true, long.MaxValue, false, []);
        Check(report.Items.Count(i => i.Status == "Conflict") == 2, "conflicting package selections are blocked");
        report = await PreflightService.CheckAsync(["Google Chrome"], "IT", store, true, true, long.MaxValue, false, [], ["PG-wallpaper.jpeg"]);
        Check(report.HasSystemBlocker, "selected Windows settings require their own assets");
        var missingCatalog = new AssetStore(fixture, cache, Console.WriteLine);
        report = await PreflightService.CheckAsync(["Google Chrome"], "IT", missingCatalog, true, true, long.MaxValue, false, []);
        Check(report.HasSystemBlocker && report.ReadyPackages.Count == 0, "missing catalog blocks deployment");
        Check(PreflightService.EstimateRequiredBytes([catalog.Files[0], catalog.Files[0]]) == original.Length * 4L + 1073741824, "disk estimate deduplicates shared dependencies");
        catalog.ReleaseVersion = "4.0.0";
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(catalog));
        report = await PreflightService.CheckAsync(["Google Chrome"], "IT", new AssetStore(app, cache, Console.WriteLine), true, true, long.MaxValue, false, []);
        Check(report.HasSystemBlocker, "a catalog from another application release cannot be deployed");
        catalog.ReleaseVersion = "4.1.0";
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(catalog));
        var fingerprint = catalog.Fingerprint;
        catalog.Files[0] = catalog.Files[0] with { Version = "2.0" };
        Check(fingerprint != catalog.Fingerprint, "catalog version changes invalidate the deployment fingerprint");
        foreach (var name in new[] { "7z.exe", "7z.dll" }) File.Copy(Path.Combine(_root, "PGInstaller", name), Path.Combine(app, name));
        var archive = Path.Combine(app, "assets.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("assets/chrome.exe").Open()); writer.Write(original);
        }
        Directory.CreateDirectory(Path.Combine(cache, "Packages", "assets"));
        var cached = Path.Combine(cache, "Packages", "assets", "chrome.exe");
        await File.WriteAllTextAsync(cached, new string('x', original.Length));
        File.Delete(Path.Combine(source, "chrome.exe"));
        store.ResetVerification();
        Check(await store.EnsureAsync("chrome.exe") != null && await File.ReadAllTextAsync(cached) == original, "invalid cached installer is selectively restored from the archive");
        catalog.Files[0] = catalog.Files[0] with { Sha256 = new string('0', 64) };
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(catalog));
        var wrongCatalog = new AssetStore(app, cache, Console.WriteLine);
        Check(await wrongCatalog.EnsureAsync("chrome.exe") == null && !File.Exists(cached), "archive content with an unexpected digest cannot be used");
        catalog.Files.Add(catalog.Files[0]);
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(catalog));
        Check(new AssetStore(app, cache, Console.WriteLine).Manifest == null, "duplicate catalog entries are rejected");
    }

    private static async Task CheckJournalAndResumeAsync()
    {
        var fixture = Path.Combine(_root, "artifacts", "checks", "journal-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(fixture, "state.json");
        var journal = new InstallJournal(path);
        var state = new DeploymentState
        {
            CatalogFingerprint = "catalog-A", BootId = "boot-A", Options = new() { SetWallpaper = true },
            Packages = [new() { Name = "Google Chrome" }, new() { Name = "Mozilla Firefox" }, new() { Name = "WinRAR" }]
        };
        var ran = new List<string>();
        int saves = 0;
        await InstallationRunner.RunAsync(state, state.Packages.Select(p => p.Name).ToArray(), "boot-A", package =>
        {
            Check(JsonSerializer.Deserialize<DeploymentState>(File.ReadAllText(path))!.Packages.Single(p => p.Name == package.Name).Status == PackageStatus.InProgress,
                "checkpoint is on disk before a package executes");
            ran.Add(package.Name);
            return Task.FromResult(new PackageRunResult(true, RestartRequired: true));
        }, () => { journal.Save(state); saves++; }, () => { });
        Check(ran.SequenceEqual(["Google Chrome"]) && state.RebootPending && saves == 2, "restart request saves the completed package and pauses the queue");
        Check(!state.CanResume("boot-A") && state.CanResume("boot-B"), "resume waits for an actual change of boot session");
        var restored = journal.Load()!;
        Check(restored.Options.SetWallpaper && !restored.Options.DisableFirewall && restored.CatalogFingerprint == "catalog-A", "checkpoint restores the exact optional settings and catalog fingerprint");
        restored.MachineName = "PREVIOUS-PC-NAME";
        journal.Save(restored);
        Check(journal.Load() != null, "renaming the computer does not invalidate its saved deployment");
        ran.Clear();
        await InstallationRunner.RunAsync(restored, restored.Packages.Where(p => p.Status == PackageStatus.Pending).Select(p => p.Name).ToArray(), "boot-B", package =>
        {
            ran.Add(package.Name);
            return Task.FromResult(new PackageRunResult(package.Name != "Mozilla Firefox", Error: package.Name == "Mozilla Firefox" ? "Simulated failure" : null));
        }, () => journal.Save(restored), () => { });
        Check(ran.SequenceEqual(["Mozilla Firefox", "WinRAR"]) && restored.Packages[0].Attempts == 1, "resume runs only pending packages and preserves successful work");
        ran.Clear();
        var failed = restored.Packages.Where(p => p.Status == PackageStatus.Failed).Select(p => p.Name).ToArray();
        await InstallationRunner.RunAsync(restored, failed, "boot-B", package =>
        {
            ran.Add(package.Name); return Task.FromResult(new PackageRunResult(true));
        }, () => journal.Save(restored), () => { });
        Check(ran.SequenceEqual(["Mozilla Firefox"]) && restored.Packages[1].Attempts == 2, "retry targets failed packages without reinstalling successful packages");
        restored.Packages[1].Status = PackageStatus.InProgress;
        journal.Save(restored);
        Check(journal.Load()!.Packages[1].Status == PackageStatus.Pending, "interrupted in-progress work becomes resumable");
        restored.Packages[1].Status = PackageStatus.Failed;
        journal.Save(restored);
        await File.WriteAllTextAsync(path, "partial JSON");
        Check(journal.Load() != null, "previous checkpoint recovers a damaged latest state file");
        journal.Save(restored);
        await File.WriteAllTextAsync(path, "partial JSON again");
        Check(journal.Load() != null, "saving after recovery preserves the last valid backup");
        var pending = new DeploymentState { Packages = [new() { Name = "FSDM" }] };
        await InstallationRunner.RunAsync(pending, ["FSDM"], "boot-A", _ => Task.FromResult(new PackageRunResult(false, RestartRequired: true)), () => { }, () => { });
        Check(pending.Packages[0].Status == PackageStatus.Pending && pending.CanResume("boot-B"), "a package interrupted by a prerequisite restart remains pending");
        var individual = new DeploymentState { Packages = [new() { Name = "Mozilla Firefox", Status = PackageStatus.Failed }, new() { Name = "WinRAR", Status = PackageStatus.Failed }] };
        await InstallationRunner.RunAsync(individual, ["WinRAR"], "boot-A", _ => Task.FromResult(new PackageRunResult(true)), () => { }, () => { });
        Check(individual.Packages[0].Status == PackageStatus.Failed && individual.Packages[0].Attempts == 0 && individual.Packages[1].Status == PackageStatus.Succeeded,
            "individual retry leaves other failures untouched");
        bool executed = false;
        try
        {
            await InstallationRunner.RunAsync(new() { Packages = [new() { Name = "WinRAR" }] }, ["WinRAR"], "boot-A",
                _ => { executed = true; return Task.FromResult(new PackageRunResult(true)); }, () => throw new IOException("Simulated checkpoint failure"), () => { });
        }
        catch (IOException) { }
        Check(!executed, "checkpoint write failure prevents package execution");
    }
}
