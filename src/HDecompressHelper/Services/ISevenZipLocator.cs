namespace HDecompressHelper.Services;

public interface ISevenZipLocator
{
    string? ExecutablePath { get; }

    string? Locate();

    bool TryUse(string executablePath);
}

