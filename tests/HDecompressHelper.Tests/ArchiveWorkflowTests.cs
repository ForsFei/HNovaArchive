using System.IO.Compression;
using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;
using HDecompressHelper.Services;
using HDecompressHelper.ViewModels;

namespace HDecompressHelper.Tests;

public sealed class ArchiveWorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HNovaWorkflow-" + Guid.NewGuid().ToString("N"));
    private string State => Path.Combine(_root, "state", "workflow.json");

    [Fact]
    public void MonitoringTimerStartsOnUiDispatcher()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new JsonSettingsService(Path.Combine(_root, "configuration"));
                using var queue = new ExtractionQueueService(new ExtractionServiceRouter([]));
                using var vm = new MainViewModel(queue, new PasswordRetryService(queue), new FileExplorerService(),
                    new FileDialogService(), new SevenZipLocator(), new WinRarLocator(), settings,
                    new JsonHistoryService(Path.Combine(_root, "configuration")), new StubTheme());
                var timer = (System.Windows.Threading.DispatcherTimer) typeof(MainViewModel)
                    .GetField("_workflowTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(vm)!;
                Assert.True(timer.IsEnabled);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (error is not null) throw error;
    }

    private sealed class StubTheme : IThemeService
    {
        public AppTheme CurrentTheme { get; private set; }
        public void Apply(AppTheme theme) => CurrentTheme = theme;
    }

    [Fact]
    public void DispatcherWorkflowAutomaticallyQueuesStableZip()
    {
        var locator = new SevenZipLocator();
        if (locator.Locate() is null) return;
        Zip("automatic.zip");
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
            using var queue = new ExtractionQueueService(new ExtractionServiceRouter([new SevenZipExtractionService(locator)]));
            using var vm = new MainViewModel(queue, new PasswordRetryService(queue), new FileExplorerService(),
                new FileDialogService(), locator, new WinRarLocator(), new JsonSettingsService(Path.Combine(_root, "config")),
                new JsonHistoryService(Path.Combine(_root, "config")), new StubTheme());
            typeof(MainViewModel).GetField("_workflowScanner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(vm, new ArchiveWorkflowScanner(State));
            vm.WatchedFolders.Add(_root);
            vm.FolderMonitoringEnabled = true;
            var finish = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromSeconds(13), System.Windows.Threading.DispatcherPriority.Normal,
                (_, _) => dispatcher.InvokeShutdown(), dispatcher);
            dispatcher.UnhandledException += (_, e) => { error = e.Exception; e.Handled = true; dispatcher.InvokeShutdown(); };
            System.Windows.Threading.Dispatcher.Run();
            finish.Stop();
            try { Assert.Contains(vm.Tasks, t => t.Status == ExtractionStatus.Completed); }
            catch (Exception ex) { error = new Exception(vm.WorkflowStatus, ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (error is not null) throw error;
    }
    public ArchiveWorkflowTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private string Zip(string name, string entry = "hello.txt")
    {
        var path = Path.Combine(_root, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
        writer.Write("payload");
        return path;
    }

    [Fact]
    public void DetectsDisguisedArchiveButLeavesNormalVideoAlone()
    {
        var disguised = Zip("secret.mp4");
        var video = Path.Combine(_root, "movie.mp4");
        File.WriteAllText(video, "ordinary media data");
        Assert.True(ArchiveWorkflowScanner.IsCandidate(disguised));
        Assert.False(ArchiveWorkflowScanner.IsCandidate(video));
        Assert.Equal("movie.mp4", Path.GetFileName(video));
    }

    [Fact]
    public void UnrelatedChangingFileDoesNotBlockStableZip()
    {
        var archive = Zip("ready.zip");
        var download = Path.Combine(_root, "other.download");
        File.WriteAllText(download, "partial");
        var scanner = new ArchiveWorkflowScanner(State);
        var now = DateTimeOffset.UtcNow;
        scanner.Scan([_root], [], now, default);
        File.AppendAllText(download, "more bytes");
        Assert.Contains(archive, scanner.Scan([_root], [], now.AddSeconds(6), default));
    }

    [Theory]
    [InlineData("data.7z.001", true)]
    [InlineData("data.7z.002", false)]
    [InlineData("data.zip.003", false)]
    [InlineData("data.7z.11111", true)]
    [InlineData("data.part01.rar", true)]
    [InlineData("data.part02.rar", false)]
    [InlineData("data.part10.rar", false)]
    [InlineData("data.r00", false)]
    public void FiltersSecondaryVolumes(string name, bool expected)
    {
        var file = Path.Combine(_root, name);
        File.WriteAllText(file, "fixture");
        Assert.Equal(expected, ArchiveWorkflowScanner.IsCandidate(file));
    }

    [Fact]
    public void WaitsForStabilityAndPersistsProcessedFilesOutsideHistory()
    {
        var archive = Zip("example.zip");
        var now = DateTimeOffset.UtcNow;
        var scanner = new ArchiveWorkflowScanner(State);
        Assert.Empty(scanner.Scan([_root], [], now, default));
        Assert.Contains(archive, scanner.Scan([_root], [], now.AddSeconds(6), default));
        scanner.MarkProcessed(archive, ArchiveWorkflowScanner.Stamp(archive));
        var restarted = new ArchiveWorkflowScanner(State);
        restarted.Scan([_root], [], now, default);
        Assert.DoesNotContain(archive, restarted.Scan([_root], [], now.AddSeconds(6), default));
        File.SetLastWriteTimeUtc(archive, DateTime.UtcNow.AddMinutes(1));
        Assert.DoesNotContain(archive, restarted.Scan([_root], [], now.AddSeconds(7), default));
        Assert.Contains(archive, restarted.Scan([_root], [], now.AddSeconds(13), default));
    }

    [Fact]
    public void ExcludesActiveOutputAndDeduplicatesOverlappingRoots()
    {
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        var archive = Zip("test.zip");
        var nested = Path.Combine(output, "child.zip");
        File.Copy(archive, nested);
        var scanner = new ArchiveWorkflowScanner(State);
        var now = DateTimeOffset.UtcNow;
        scanner.Scan([_root, output], [output], now, default);
        var ready = scanner.Scan([_root, output], [output], now.AddSeconds(6), default);
        Assert.Equal([archive], ready);
        scanner.Scan([_root, output], [], now.AddSeconds(7), default);
        ready = scanner.Scan([_root, output], [], now.AddSeconds(13), default);
        Assert.Equal(2, ready.Count);
        Assert.Contains(nested, ready);
    }

    [Fact]
    public async Task ExtractedNestedDisguisedArchiveIsDiscoveredAndExtracted()
    {
        var locator = new SevenZipLocator();
        if (locator.Locate() is null) return;
        var inner = Zip("inner.mp4");
        var outer = Path.Combine(_root, "outer.zip");
        using (var zip = ZipFile.Open(outer, ZipArchiveMode.Create)) zip.CreateEntryFromFile(inner, "inner.mp4");
        File.Delete(inner);
        var service = new SevenZipExtractionService(locator);
        var output = Path.Combine(_root, "outer");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await service.ExtractAsync(new ExtractionRequest(Guid.NewGuid(), outer, output), null, null, timeout.Token);
        Assert.True(result.IsSuccessful);
        var scanner = new ArchiveWorkflowScanner(State);
        var now = DateTimeOffset.UtcNow;
        scanner.MarkProcessed(outer, ArchiveWorkflowScanner.Stamp(outer));
        scanner.Scan([_root], [], now, default);
        var ready = scanner.Scan([_root], [], now.AddSeconds(6), default);
        var nested = Assert.Single(ready);
        Assert.EndsWith("inner.mp4", nested);
        var prepared = ArchiveInputNormalizer.Prepare(nested);
        Assert.EndsWith("inner.zip", prepared.ArchivePath);
        result = await service.ExtractAsync(new ExtractionRequest(Guid.NewGuid(), prepared.ArchivePath,
            ArchivePathHelper.GetDefaultDestination(prepared.ArchivePath)), null, null, timeout.Token);
        Assert.True(result.IsSuccessful);
        Assert.True(File.Exists(Path.Combine(output, "inner", "hello.txt")));
    }

    [Fact]
    public void PasswordPromptRequiresPasswordEvidenceNotGenericFatalSummary()
    {
        var result = new ExtractionResult(ExtractionEngine.SevenZip, 2, ExtractionExitKind.FatalError,
            "7-Zip 报告致命错误，压缩包可能损坏或密码不正确", "", "", "Headers Error");
        Assert.False(MainViewModel.NeedsPasswordPrompt(result));
        Assert.True(MainViewModel.NeedsPasswordPrompt(result with { StandardError = "Wrong password?" }));
    }
}
