namespace HDecompressHelper.Models;

public sealed record PasswordAttemptResult(
    int AttemptNumber,
    string Password,
    ExtractionResult Result);
