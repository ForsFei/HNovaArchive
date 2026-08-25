using HDecompressHelper.Models;
using HDecompressHelper.Services;

namespace HDecompressHelper.Tests;

public sealed class PasswordRetryServiceTests
{
    [Fact]
    public void CandidateListKeepsCurrentFirstAndDeduplicatesCaseSensitively()
    {
        var candidates = PasswordRetryService.BuildCandidateList(
            "Current",
            ["saved", "Current", "saved", "SAVED", ""]);

        Assert.Equal(["Current", "saved", "SAVED"], candidates);
    }

    [Fact]
    public async Task RetriesFatalFailuresUntilSavedPasswordSucceeds()
    {
        using var queue = new RecordingQueue(request =>
            string.Equals(request.Password, "correct", StringComparison.Ordinal)
                ? Result(ExtractionExitKind.Success)
                : Result(ExtractionExitKind.FatalError));
        var service = new PasswordRetryService(queue);

        var outcome = await service.RunAsync(
            new ExtractionRequest(Guid.NewGuid(), "archive.7z", "output", Password: "wrong"),
            ["wrong", "also-wrong", "correct", "unused"],
            progress: null,
            lifecycle: null,
            attemptLifecycle: null,
            CancellationToken.None);

        Assert.Equal(ExtractionExitKind.Success, outcome.FinalResult.ExitKind);
        Assert.Equal("correct", outcome.EffectivePassword);
        Assert.Equal(["wrong", "also-wrong", "correct"], queue.Passwords);
        Assert.Equal(3, outcome.Attempts.Count);
    }

    [Fact]
    public async Task StopsImmediatelyForNonRetryableFailure()
    {
        using var queue = new RecordingQueue(_ => Result(ExtractionExitKind.WriteError));
        var service = new PasswordRetryService(queue);

        var outcome = await service.RunAsync(
            new ExtractionRequest(Guid.NewGuid(), "archive.7z", "output", Password: "wrong"),
            ["saved-one", "saved-two"],
            progress: null,
            lifecycle: null,
            attemptLifecycle: null,
            CancellationToken.None);

        Assert.Equal(ExtractionExitKind.WriteError, outcome.FinalResult.ExitKind);
        Assert.Equal(["wrong"], queue.Passwords);
        Assert.Single(outcome.Attempts);
    }

    private static ExtractionResult Result(ExtractionExitKind kind) => new(
        ExtractionEngine.SevenZip,
        kind == ExtractionExitKind.Success ? 0 : 2,
        kind,
        kind.ToString(),
        "7z",
        string.Empty,
        string.Empty);

    private sealed class RecordingQueue(Func<ExtractionRequest, ExtractionResult> resultFactory) : IExtractionQueueService
    {
        public List<string> Passwords { get; } = [];

        public int MaxConcurrentExtractions => 1;

        public Task<ExtractionResult> RunAsync(
            ExtractionRequest request,
            IProgress<ExtractionProgress>? progress,
            IProgress<ExtractionLifecycle>? lifecycle,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Passwords.Add(request.Password ?? string.Empty);
            return Task.FromResult(resultFactory(request));
        }

        public void Dispose()
        {
        }
    }
}
