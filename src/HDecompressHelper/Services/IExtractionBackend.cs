using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public interface IExtractionBackend
{
    ExtractionEngine Engine { get; }

    Task<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        CancellationToken cancellationToken);
}

