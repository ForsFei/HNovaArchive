using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public interface IFileDialogService
{
    IReadOnlyList<string> PickArchives();

    string? PickExtractionExecutable(ExtractionEngine engine);
}
