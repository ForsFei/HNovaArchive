namespace HDecompressHelper.Services;

public sealed class WinRarLocator : IWinRarLocator
{
    private static readonly HashSet<string> SupportedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rar.exe",
        "UnRAR.exe",
        "WinRAR.exe"
    };

    private string? _selectedPath;

    public string? ExecutablePath => IsExecutable(_selectedPath) ? _selectedPath : Locate();

    public string? Locate()
    {
        var candidates = new List<string?> { _selectedPath };
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     AppContext.BaseDirectory
                 })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var folder = root == AppContext.BaseDirectory
                ? Path.Combine(root, "Tools", "WinRAR")
                : Path.Combine(root, "WinRAR");
            candidates.Add(Path.Combine(folder, "Rar.exe"));
            candidates.Add(Path.Combine(folder, "UnRAR.exe"));
            candidates.Add(Path.Combine(folder, "WinRAR.exe"));
        }

        var pathEnvironment = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnvironment))
        {
            foreach (var folder in pathEnvironment.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                candidates.Add(Path.Combine(folder, "Rar.exe"));
                candidates.Add(Path.Combine(folder, "UnRAR.exe"));
                candidates.Add(Path.Combine(folder, "WinRAR.exe"));
            }
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
        SupportedNames.Contains(Path.GetFileName(path));
}

