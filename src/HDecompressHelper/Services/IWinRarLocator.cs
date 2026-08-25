namespace HDecompressHelper.Services;

public interface IWinRarLocator
{
    string? ExecutablePath { get; }

    string? Locate();

    bool TryUse(string executablePath);
}

