using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public interface IPasswordRetryService
{
    Task<PasswordRetryResult> RunAsync(
        ExtractionRequest request,
        IEnumerable<string> savedPasswords,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        IProgress<PasswordAttemptLifecycle>? attemptLifecycle,
        CancellationToken cancellationToken);
}
