namespace HDecompressHelper.Models;

public sealed class ExtractionHistoryRecord
{
    public Guid Id { get; set; }

    public string ArchivePath { get; set; } = string.Empty;

    public string DestinationPath { get; set; } = string.Empty;

    public string ArchiveName { get; set; } = string.Empty;

    public long ArchiveSize { get; set; }

    public ExtractionEngine Engine { get; set; } = ExtractionEngine.SevenZip;

    public ExtractionStatus Status { get; set; }

    public int Progress { get; set; }

    public string? CurrentFile { get; set; }

    public DateTimeOffset? StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public string? ErrorMessage { get; set; }

    public string DetailText { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

