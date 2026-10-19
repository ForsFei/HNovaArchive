using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;

namespace HDecompressHelper.Tests;

public sealed class PasswordFailureClassifierTests
{
    [Fact]
    public void SevenZipPromptWithoutPasswordBecomesActionableFailure()
    {
        var result = Result(
            ExtractionEngine.SevenZip,
            255,
            ExtractionExitKind.Stopped,
            standardOutput: "Enter password (will not be echoed):");

        var normalized = PasswordFailureClassifier.NormalizeMissingPassword(result, string.Empty);

        Assert.Equal(ExtractionExitKind.FatalError, normalized.ExitKind);
        Assert.Equal(PasswordFailureClassifier.MissingPasswordSummary, normalized.Summary);
    }

    [Fact]
    public void WinRarPasswordExitWithoutPasswordBecomesActionableFailure()
    {
        var result = Result(
            ExtractionEngine.WinRar,
            11,
            ExtractionExitKind.FatalError);

        var normalized = PasswordFailureClassifier.NormalizeMissingPassword(result, null);

        Assert.Equal(ExtractionExitKind.FatalError, normalized.ExitKind);
        Assert.Equal(PasswordFailureClassifier.MissingPasswordSummary, normalized.Summary);
    }

    [Fact]
    public void ExplicitPasswordKeepsOriginalFailure()
    {
        var result = Result(
            ExtractionEngine.SevenZip,
            2,
            ExtractionExitKind.FatalError,
            standardError: "Wrong password");

        var normalized = PasswordFailureClassifier.NormalizeMissingPassword(result, "wrong");

        Assert.Same(result, normalized);
    }

    private static ExtractionResult Result(
        ExtractionEngine engine,
        int exitCode,
        ExtractionExitKind exitKind,
        string standardOutput = "",
        string standardError = "") => new(
            engine,
            exitCode,
            exitKind,
            "original summary",
            "command",
            standardOutput,
            standardError);
}
