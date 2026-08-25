namespace HDecompressHelper.Models;

public enum ExtractionExitKind
{
    Success,
    Warning,
    FatalError,
    CommandLineError,
    OutOfMemory,
    Stopped,
    ReadError,
    WriteError,
    OpenError,
    UserError,
    UnknownError
}

public sealed record ExtractionResult(
    ExtractionEngine Engine,
    int ExitCode,
    ExtractionExitKind ExitKind,
    string Summary,
    string Command,
    string StandardOutput,
    string StandardError)
{
    public bool IsSuccessful => ExitKind is ExtractionExitKind.Success or ExtractionExitKind.Warning;
}
