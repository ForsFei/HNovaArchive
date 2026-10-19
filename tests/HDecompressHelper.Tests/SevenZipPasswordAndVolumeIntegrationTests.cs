using System.Diagnostics;
using HDecompressHelper.Models;
using HDecompressHelper.Services;

namespace HDecompressHelper.Tests;

public sealed class SevenZipPasswordAndVolumeIntegrationTests
{
    [Fact]
    public async Task MissingPasswordFailsPromptlyWithActionableMessage()
    {
        var locator = new SevenZipLocator();
        var executable = locator.Locate();
        if (executable is null)
        {
            return;
        }

        var directory = CreateTemporaryDirectory();
        try
        {
            const string correctPassword = "correct-integration-password";
            var input = Path.Combine(directory, "secret.txt");
            var archive = Path.Combine(directory, "secure.7z");
            var output = Path.Combine(directory, "output");
            await File.WriteAllTextAsync(input, "encrypted payload");
            await RunSevenZipAsync(executable, "a", archive, input, $"-p{correctPassword}", "-mhe=on", "-y");

            var service = new SevenZipExtractionService(locator);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await service.ExtractAsync(
                new ExtractionRequest(Guid.NewGuid(), archive, output, Password: string.Empty),
                progress: null,
                lifecycle: null,
                timeout.Token);

            Assert.Equal(ExtractionExitKind.FatalError, result.ExitKind);
            Assert.Contains("需要密码", result.Summary, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WrongPasswordRetriesSavedPasswordsAndPersistsEffectivePassword()
    {
        var locator = new SevenZipLocator();
        var executable = locator.Locate();
        if (executable is null)
        {
            return;
        }

        var directory = CreateTemporaryDirectory();
        try
        {
            const string correctPassword = "correct-integration-password";
            var input = Path.Combine(directory, "secret.txt");
            var archive = Path.Combine(directory, "secure.7z");
            var output = Path.Combine(directory, "output");
            await File.WriteAllTextAsync(input, "encrypted payload");
            await RunSevenZipAsync(executable, "a", archive, input, $"-p{correctPassword}", "-mhe=on", "-y");

            var router = new ExtractionServiceRouter([new SevenZipExtractionService(locator)]);
            using var queue = new ExtractionQueueService(router, maxConcurrentExtractions: 1);
            var retryService = new PasswordRetryService(queue);

            var result = await retryService.RunAsync(
                new ExtractionRequest(
                    Guid.NewGuid(),
                    archive,
                    output,
                    ExtractionEngine.SevenZip,
                    "wrong-password"),
                ["another-wrong-password", correctPassword],
                progress: null,
                lifecycle: null,
                attemptLifecycle: null,
                CancellationToken.None);

            Assert.Equal(ExtractionExitKind.Success, result.FinalResult.ExitKind);
            Assert.Equal(correctPassword, result.EffectivePassword);
            Assert.Equal(3, result.Attempts.Count);
            Assert.True(File.Exists(Path.Combine(output, "secret.txt")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractsSevenZipFirstVolumeDirectly()
    {
        var locator = new SevenZipLocator();
        var executable = locator.Locate();
        if (executable is null)
        {
            return;
        }

        var directory = CreateTemporaryDirectory();
        try
        {
            var input = Path.Combine(directory, "volume-content.bin");
            var archive = Path.Combine(directory, "volume.7z");
            var firstVolume = archive + ".001";
            var output = Path.Combine(directory, "output");
            await File.WriteAllBytesAsync(input, Enumerable.Range(0, 32 * 1024)
                .Select(index => (byte)(index % 251))
                .ToArray());
            await RunSevenZipAsync(executable, "a", archive, input, "-v4k", "-mx=0", "-y");

            Assert.True(File.Exists(firstVolume));
            var service = new SevenZipExtractionService(locator);
            var result = await service.ExtractAsync(
                new ExtractionRequest(Guid.NewGuid(), firstVolume, output),
                progress: null,
                lifecycle: null,
                CancellationToken.None);

            Assert.Equal(ExtractionExitKind.Success, result.ExitKind);
            Assert.True(File.Exists(Path.Combine(output, "volume-content.bin")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task RunSevenZipAsync(
        string executable,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 7-Zip 集成测试进程。");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await standardOutput;
        var error = await standardError;
        Assert.True(
            process.ExitCode == 0,
            $"7-Zip fixture creation failed ({process.ExitCode}).{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"HDecompressHelper.Encrypted-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
