using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public sealed class PasswordRetryService(IExtractionQueueService queueService) : IPasswordRetryService
{
    public async Task<PasswordRetryResult> RunAsync(
        ExtractionRequest request,
        IEnumerable<string> savedPasswords,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        IProgress<PasswordAttemptLifecycle>? attemptLifecycle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(savedPasswords);

        var passwords = BuildCandidateList(request.Password, savedPasswords);
        var attempts = new List<PasswordAttemptResult>(passwords.Count);

        for (var index = 0; index < passwords.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var password = passwords[index];
            attemptLifecycle?.Report(new PasswordAttemptLifecycle(index + 1, passwords.Count, password));

            var result = await queueService.RunAsync(
                request with { Password = password },
                progress,
                lifecycle,
                cancellationToken);
            attempts.Add(new PasswordAttemptResult(index + 1, password, result));

            if (result.IsSuccessful || !ShouldTryAnotherPassword(result))
            {
                return new PasswordRetryResult(result, password, attempts);
            }
        }

        var finalAttempt = attempts[^1];
        return new PasswordRetryResult(finalAttempt.Result, finalAttempt.Password, attempts);
    }

    internal static IReadOnlyList<string> BuildCandidateList(
        string? initialPassword,
        IEnumerable<string> savedPasswords)
    {
        var initial = initialPassword ?? string.Empty;
        var result = new List<string> { initial };
        var seen = new HashSet<string>(StringComparer.Ordinal) { initial };

        foreach (var password in savedPasswords)
        {
            if (!string.IsNullOrEmpty(password) && seen.Add(password))
            {
                result.Add(password);
            }
        }

        return result;
    }

    internal static bool ShouldTryAnotherPassword(ExtractionResult result) =>
        result.ExitKind is ExtractionExitKind.FatalError or ExtractionExitKind.UnknownError;
}
