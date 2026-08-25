using HDecompressHelper.Models;
using HDecompressHelper.Services;

namespace HDecompressHelper.Tests;

public sealed class JsonPersistenceTests
{
    [Fact]
    public void SettingsRoundTripIncludesPlaintextPassword()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            const string password = "plain-text-secret-123";
            var service = new JsonSettingsService(directory);
            service.Save(new AppSettings
            {
                Theme = AppTheme.Light,
                ViewMode = TaskViewMode.Tiles,
                DefaultEngine = ExtractionEngine.WinRar,
                SevenZipPath = @"C:\Tools\7z.exe",
                WinRarPath = @"C:\Tools\Rar.exe",
                UsePassword = true,
                IsPasswordVisible = true,
                Password = password,
                SavedPasswords = ["first", password]
            });

            var restored = service.Load();

            Assert.Equal(AppTheme.Light, restored.Theme);
            Assert.Equal(TaskViewMode.Tiles, restored.ViewMode);
            Assert.Equal(ExtractionEngine.WinRar, restored.DefaultEngine);
            Assert.True(restored.UsePassword);
            Assert.True(restored.IsPasswordVisible);
            Assert.Equal(password, restored.Password);
            Assert.Equal(["first", password], restored.SavedPasswords);
            Assert.Contains(password, File.ReadAllText(service.FilePath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void HistoryRoundTripIncludesPlaintextPasswordAndDetailLog()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            const string password = "history-password-456";
            var record = new ExtractionHistoryRecord
            {
                Id = Guid.NewGuid(),
                ArchivePath = @"C:\Archives\demo.rar",
                DestinationPath = @"C:\Archives\demo",
                ArchiveName = "demo.rar",
                ArchiveSize = 12345,
                Engine = ExtractionEngine.WinRar,
                Status = ExtractionStatus.Completed,
                Progress = 100,
                StartTime = DateTimeOffset.Parse("2026-08-25T08:00:00+08:00"),
                EndTime = DateTimeOffset.Parse("2026-08-25T08:00:05+08:00"),
                DetailText = $"PASSWORD (PLAINTEXT): {password}",
                Password = password
            };
            var service = new JsonHistoryService(directory);

            service.Save([record]);
            var restored = Assert.Single(service.Load());

            Assert.Equal(record.Id, restored.Id);
            Assert.Equal(ExtractionEngine.WinRar, restored.Engine);
            Assert.Equal(ExtractionStatus.Completed, restored.Status);
            Assert.Equal(password, restored.Password);
            Assert.Contains(password, restored.DetailText, StringComparison.Ordinal);
            Assert.Contains(password, File.ReadAllText(service.FilePath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CorruptJsonFallsBackToDefaults()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var settings = new JsonSettingsService(directory);
            var history = new JsonHistoryService(directory);
            File.WriteAllText(settings.FilePath, "{not valid json");
            File.WriteAllText(history.FilePath, "[not valid json");

            var restoredSettings = settings.Load();

            Assert.Equal(AppTheme.Dark, restoredSettings.Theme);
            Assert.Equal(TaskViewMode.List, restoredSettings.ViewMode);
            Assert.Equal(ExtractionEngine.SevenZip, restoredSettings.DefaultEngine);
            Assert.Empty(history.Load());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"HDecompressHelper.Persistence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
