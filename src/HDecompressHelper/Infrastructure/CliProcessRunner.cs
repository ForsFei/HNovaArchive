using System.Buffers;
using System.Diagnostics;
using HDecompressHelper.Models;

namespace HDecompressHelper.Infrastructure;

internal static class CliProcessRunner
{
    private const int LogCapacity = 512 * 1024;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(75);

    public static async Task<ExtractionResult> RunAsync(
        ExtractionEngine engine,
        ProcessStartInfo startInfo,
        string displayCommand,
        IProgressStreamParser parser,
        Func<int, (ExtractionExitKind Kind, string Summary)> exitCodeMapper,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        CancellationToken cancellationToken)
    {
        var stdout = new CappedLogBuffer(LogCapacity);
        var stderr = new CappedLogBuffer(LogCapacity);
        var throttler = new ProgressThrottler(progress, ProgressInterval);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"无法启动 {engine} 解压进程。");
        }

        lifecycle?.Report(new ExtractionLifecycle(
            ExtractionLifecycleStage.ProcessStarted,
            process.Id));

        using var cancellationRegistration = cancellationToken.Register(
            static state => TryKill((Process)state!),
            process);

        var stdoutTask = PumpOutputAsync(process.StandardOutput, parser, stdout, throttler);
        var stderrTask = PumpLogAsync(process.StandardError, stderr);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            throw;
        }

        await Task.WhenAll(stdoutTask, stderrTask);
        throttler.Flush();

        var (kind, summary) = exitCodeMapper(process.ExitCode);
        return new ExtractionResult(
            engine,
            process.ExitCode,
            kind,
            summary,
            displayCommand,
            stdout.ToString(),
            stderr.ToString());
    }

    private static async Task PumpOutputAsync(
        StreamReader reader,
        IProgressStreamParser parser,
        CappedLogBuffer log,
        ProgressThrottler throttler)
    {
        var buffer = ArrayPool<char>.Shared.Rent(4096);
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                var chunk = new string(buffer, 0, read);
                log.Append(chunk);
                foreach (var update in parser.Append(chunk))
                {
                    throttler.Report(update);
                }
            }

            foreach (var update in parser.Complete())
            {
                throttler.Report(update);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private static async Task PumpLogAsync(StreamReader reader, CappedLogBuffer log)
    {
        var buffer = ArrayPool<char>.Shared.Rent(2048);
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                log.Append(new string(buffer, 0, read));
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process already exited between HasExited and Kill.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // A concurrently exiting process can make Kill fail; WaitForExit handles it.
        }
    }
}

