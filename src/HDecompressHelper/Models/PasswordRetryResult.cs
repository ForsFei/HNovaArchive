namespace HDecompressHelper.Models;

public sealed record PasswordRetryResult(
    ExtractionResult FinalResult,
    string EffectivePassword,
    IReadOnlyList<PasswordAttemptResult> Attempts);
