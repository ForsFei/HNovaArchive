namespace HDecompressHelper.Models;

public enum ExtractionLifecycleStage
{
    WorkerStarted,
    ProcessStarted
}

public sealed record ExtractionLifecycle(ExtractionLifecycleStage Stage, int? ProcessId = null);

