namespace HDecompressHelper.Models;

public sealed record ExtractionRequest(
    Guid TaskId,
    string ArchivePath,
    string DestinationPath,
    ExtractionEngine Engine = ExtractionEngine.SevenZip,
    string? Password = null);
