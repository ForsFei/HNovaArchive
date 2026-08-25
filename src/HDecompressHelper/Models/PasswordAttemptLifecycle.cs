namespace HDecompressHelper.Models;

public sealed record PasswordAttemptLifecycle(
    int AttemptNumber,
    int TotalAttempts,
    string Password);
