using HDecompressHelper.Models;

namespace HDecompressHelper.Infrastructure;

internal static class PasswordFailureClassifier
{
    internal const string MissingPasswordSummary =
        "压缩包需要密码，请在顶部密码框输入密码后重新添加任务";

    public static ExtractionResult NormalizeMissingPassword(
        ExtractionResult result,
        string? suppliedPassword)
    {
        if (!string.IsNullOrEmpty(suppliedPassword) || result.IsSuccessful)
        {
            return result;
        }

        var output = string.Concat(result.StandardOutput, Environment.NewLine, result.StandardError);
        var mentionsPassword = output.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                               output.Contains("密码", StringComparison.Ordinal);
        var stoppedAtPasswordPrompt = result.Engine == ExtractionEngine.SevenZip && result.ExitCode == 255;
        var winRarNeedsPassword = result.Engine == ExtractionEngine.WinRar && result.ExitCode == 11;

        if (!mentionsPassword && !stoppedAtPasswordPrompt && !winRarNeedsPassword)
        {
            return result;
        }

        return result with
        {
            ExitKind = ExtractionExitKind.FatalError,
            Summary = MissingPasswordSummary
        };
    }
}
