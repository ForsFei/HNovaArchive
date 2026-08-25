using HDecompressHelper.Models;
using HDecompressHelper.Services;

namespace HDecompressHelper.Tests;

public sealed class ExtractionQueueServiceTests
{
    [Fact]
    public async Task LimitsConcurrentExtractions()
    {
        var extractionService = new CountingExtractionService();
        using var queue = new ExtractionQueueService(extractionService, maxConcurrentExtractions: 2);

        var tasks = Enumerable.Range(0, 6)
            .Select(index => queue.RunAsync(
                new ExtractionRequest(Guid.NewGuid(), $"archive-{index}.zip", $"output-{index}"),
                progress: null,
                lifecycle: null,
                CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(2, extractionService.MaximumObservedConcurrency);
    }

    [Fact]
    public async Task CancelsAQueuedItemWithoutAffectingRunningItem()
    {
        var extractionService = new BlockingExtractionService();
        using var queue = new ExtractionQueueService(extractionService, maxConcurrentExtractions: 1);

        var running = queue.RunAsync(
            new ExtractionRequest(Guid.NewGuid(), "running.zip", "running"),
            progress: null,
            lifecycle: null,
            CancellationToken.None);
        await extractionService.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        using var queuedCancellation = new CancellationTokenSource();
        var queued = queue.RunAsync(
            new ExtractionRequest(Guid.NewGuid(), "queued.zip", "queued"),
            progress: null,
            lifecycle: null,
            queuedCancellation.Token);

        queuedCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.False(running.IsCompleted);

        extractionService.Release.TrySetResult();
        var result = await running;
        Assert.Equal(ExtractionExitKind.Success, result.ExitKind);
    }

    private sealed class CountingExtractionService : IExtractionService
    {
        private int _running;
        private int _maximumObservedConcurrency;

        public int MaximumObservedConcurrency => Volatile.Read(ref _maximumObservedConcurrency);

        public async Task<ExtractionResult> ExtractAsync(
            ExtractionRequest request,
            IProgress<ExtractionProgress>? progress,
            IProgress<ExtractionLifecycle>? lifecycle,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _running);
            UpdateMaximum(current);
            try
            {
                await Task.Delay(60, cancellationToken);
                return Success();
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }

        private void UpdateMaximum(int value)
        {
            int observed;
            do
            {
                observed = Volatile.Read(ref _maximumObservedConcurrency);
                if (value <= observed)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _maximumObservedConcurrency, value, observed) != observed);
        }
    }

    private sealed class BlockingExtractionService : IExtractionService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ExtractionResult> ExtractAsync(
            ExtractionRequest request,
            IProgress<ExtractionProgress>? progress,
            IProgress<ExtractionLifecycle>? lifecycle,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success();
        }
    }

    private static ExtractionResult Success() => new(
        ExtractionEngine.SevenZip,
        0,
        ExtractionExitKind.Success,
        "ok",
        "7z",
        string.Empty,
        string.Empty);
}
