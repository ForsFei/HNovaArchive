using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public sealed class JsonHistoryService : IHistoryService
{
    public JsonHistoryService()
        : this(JsonFileStorage.GetDefaultDataDirectory())
    {
    }

    public JsonHistoryService(string dataDirectory)
    {
        FilePath = Path.Combine(Path.GetFullPath(dataDirectory), "history.json");
    }

    public string FilePath { get; }

    public IReadOnlyList<ExtractionHistoryRecord> Load() =>
        JsonFileStorage.Load(FilePath, static () => new List<ExtractionHistoryRecord>());

    public void Save(IEnumerable<ExtractionHistoryRecord> records) =>
        JsonFileStorage.Save(FilePath, records.ToArray());
}

