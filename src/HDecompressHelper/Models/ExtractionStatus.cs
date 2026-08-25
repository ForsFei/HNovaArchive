namespace HDecompressHelper.Models;

public enum ExtractionStatus
{
    Queued,
    Extracting,
    Cancelling,
    Completed,
    CompletedWithWarning,
    Failed,
    Cancelled,
    Interrupted
}

