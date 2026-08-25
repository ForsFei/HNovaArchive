using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public interface IExtractionService
{
    Task<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        CancellationToken cancellationToken);
}

