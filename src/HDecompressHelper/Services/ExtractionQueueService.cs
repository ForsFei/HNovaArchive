using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public sealed class ExtractionQueueService : IExtractionQueueService
{
    private readonly IExtractionService _extractionService;
    private readonly SemaphoreSlim _workerGate;

    public ExtractionQueueService(IExtractionService extractionService, int maxConcurrentExtractions = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentExtractions, 1);
        _extractionService = extractionService;
        MaxConcurrentExtractions = maxConcurrentExtractions;
        _workerGate = new SemaphoreSlim(maxConcurrentExtractions, maxConcurrentExtractions);
    }

    public int MaxConcurrentExtractions { get; }

    public async Task<ExtractionResult> RunAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        CancellationToken cancellationToken)
    {
        await _workerGate.WaitAsync(cancellationToken);
        try
        {
            lifecycle?.Report(new ExtractionLifecycle(ExtractionLifecycleStage.WorkerStarted));
            return await _extractionService.ExtractAsync(
                request,
                progress,
                lifecycle,
                cancellationToken);
        }
        finally
        {
            _workerGate.Release();
        }
    }

    public void Dispose() => _workerGate.Dispose();
}

