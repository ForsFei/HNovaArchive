using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public interface ISettingsService
{
    string FilePath { get; }

    AppSettings Load();

    void Save(AppSettings settings);
}

