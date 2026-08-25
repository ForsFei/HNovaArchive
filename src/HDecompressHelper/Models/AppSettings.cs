namespace HDecompressHelper.Models;

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public TaskViewMode ViewMode { get; set; } = TaskViewMode.List;

    public ExtractionEngine DefaultEngine { get; set; } = ExtractionEngine.SevenZip;

    public string? SevenZipPath { get; set; }

    public string? WinRarPath { get; set; }

    public bool UsePassword { get; set; }

    public bool IsPasswordVisible { get; set; }

    public string Password { get; set; } = string.Empty;

    public List<string> SavedPasswords { get; set; } = [];
}
