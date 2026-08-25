using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public interface IHistoryService
{
    string FilePath { get; }

    IReadOnlyList<ExtractionHistoryRecord> Load();

    void Save(IEnumerable<ExtractionHistoryRecord> records);
}

