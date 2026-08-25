using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public sealed class JsonSettingsService : ISettingsService
{
    public JsonSettingsService()
        : this(JsonFileStorage.GetDefaultDataDirectory())
    {
    }

    public JsonSettingsService(string dataDirectory)
    {
        FilePath = Path.Combine(Path.GetFullPath(dataDirectory), "settings.json");
    }

    public string FilePath { get; }

    public AppSettings Load() => JsonFileStorage.Load(FilePath, static () => new AppSettings());

    public void Save(AppSettings settings) => JsonFileStorage.Save(FilePath, settings);
}

