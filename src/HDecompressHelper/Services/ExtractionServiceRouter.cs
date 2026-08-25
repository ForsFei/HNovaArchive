using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public sealed class ExtractionServiceRouter : IExtractionService
{
    private readonly IReadOnlyDictionary<ExtractionEngine, IExtractionBackend> _backends;

    public ExtractionServiceRouter(IEnumerable<IExtractionBackend> backends)
    {
        _backends = backends.ToDictionary(backend => backend.Engine);
    }

    public Task<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        CancellationToken cancellationToken)
    {
        if (!_backends.TryGetValue(request.Engine, out var backend))
        {
            throw new NotSupportedException($"未注册 {request.Engine} 解压后端。");
        }

        return backend.ExtractAsync(request, progress, lifecycle, cancellationToken);
    }
}

