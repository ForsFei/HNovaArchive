using System.IO.Compression;
using HDecompressHelper.Models;
using HDecompressHelper.Services;

namespace HDecompressHelper.Tests;

public sealed class SevenZipExtractionServiceIntegrationTests
{
    [Fact]
    public async Task ExtractsZipAndReportsProgressWhenSevenZipIsInstalled()
    {
        var locator = new SevenZipLocator();
        if (locator.Locate() is null)
        {
            return;
        }

        var testRoot = Path.Combine(Path.GetTempPath(), $"HDecompressHelper.Tests-{Guid.NewGuid():N}");
        var input = Path.Combine(testRoot, "input");
        var archive = Path.Combine(testRoot, "示例 归档.zip");
        var output = Path.Combine(testRoot, "output");

        try
        {
            Directory.CreateDirectory(input);
            for (var index = 1; index <= 12; index++)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(input, $"中文 文件 {index:D2}.txt"),
                    string.Concat(Enumerable.Repeat($"line {index} 中文内容{Environment.NewLine}", 300)));
            }

            ZipFile.CreateFromDirectory(input, archive, CompressionLevel.NoCompression, includeBaseDirectory: false);

            var progress = new InlineProgress<ExtractionProgress>();
            var lifecycle = new InlineProgress<ExtractionLifecycle>();
            var service = new SevenZipExtractionService(locator);

            var result = await service.ExtractAsync(
                new ExtractionRequest(Guid.NewGuid(), archive, output),
                progress,
                lifecycle,
                CancellationToken.None);

            Assert.Equal(ExtractionEngine.SevenZip, result.Engine);
            Assert.Equal(ExtractionExitKind.Success, result.ExitKind);
            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(output, "中文 文件 12.txt")));
            Assert.NotEmpty(progress.Values);
            Assert.All(progress.Values, update => Assert.InRange(update.Percent, 0, 100));
            Assert.Contains(lifecycle.Values, update =>
                update.Stage == ExtractionLifecycleStage.ProcessStarted && update.ProcessId > 0);
            Assert.Contains("Everything is Ok", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value) => Values.Add(value);
    }
}
