namespace HDecompressHelper.Services;

public sealed class SevenZipLocator : ISevenZipLocator
{
    private string? _selectedPath;

    public string? ExecutablePath =>
        IsExecutable(_selectedPath) ? _selectedPath : Locate();

    public string? Locate()
    {
        var candidates = new List<string?>
        {
            _selectedPath,
            Path.Combine(AppContext.BaseDirectory, "Tools", "7zip", "7z.exe"),
            CombineIfPresent(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            CombineIfPresent(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
        };

        var pathEnvironment = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnvironment))
        {
            candidates.AddRange(pathEnvironment
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(folder => CombineIfPresent(folder, "7z.exe")));
        }

        _selectedPath = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(IsExecutable);

        return _selectedPath;
    }

    public bool TryUse(string executablePath)
    {
        if (!IsExecutable(executablePath))
        {
            return false;
        }

        _selectedPath = Path.GetFullPath(executablePath);
        return true;
    }

    private static bool IsExecutable(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        File.Exists(path) &&
        string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);

    private static string? CombineIfPresent(string? root, params string[] parts)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        return parts.Aggregate(root, Path.Combine);
    }
}

