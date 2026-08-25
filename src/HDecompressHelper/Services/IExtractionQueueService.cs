using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public interface IExtractionQueueService : IDisposable
{
    int MaxConcurrentExtractions { get; }

    Task<ExtractionResult> RunAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        CancellationToken cancellationToken);
}

