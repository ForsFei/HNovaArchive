using HDecompressHelper.Infrastructure;

namespace HDecompressHelper.Tests;

public sealed class ArchiveInputNormalizerTests
{
    [Theory]
    [InlineData("111.7z.001")]
    [InlineData("111.ZIP.001")]
    [InlineData("111.rar.001")]
    [InlineData("regular.7z")]
    [InlineData("backup.tar.gz")]
    public void RecognizesDirectlyLoadableArchiveNames(string fileName)
    {
        Assert.True(ArchiveInputNormalizer.IsLoadableArchivePath(fileName));
    }

    [Theory]
    [InlineData(@"C:\Temp\111.7z.11111", @"C:\Temp\111.7z")]
    [InlineData(@"C:\Temp\backup.tar.gz.random", @"C:\Temp\backup.tar.gz")]
    [InlineData(@"C:\Temp\111.mp4", @"C:\Temp\111.zip")]
    [InlineData(@"C:\Temp\no-extension", @"C:\Temp\no-extension.zip")]
    public void CalculatesRequestedNormalizedName(string input, string expected)
    {
        Assert.Equal(expected, ArchiveInputNormalizer.GetNormalizedPath(input));
    }

    [Theory]
    [InlineData("masked.7z.11111", "masked.7z")]
    [InlineData("video.mp4", "video.zip")]
    public void RenamesUnsupportedInputWithoutCopying(string inputName, string expectedName)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var input = Path.Combine(directory, inputName);
            File.WriteAllText(input, "archive-like payload");

            var result = ArchiveInputNormalizer.Prepare(input);

            Assert.True(result.WasRenamed);
            Assert.Equal(Path.Combine(directory, expectedName), result.ArchivePath);
            Assert.False(File.Exists(input));
            Assert.True(File.Exists(result.ArchivePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void KeepsSevenZipFirstVolumeUnchanged()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var input = Path.Combine(directory, "volume.7z.001");
            File.WriteAllText(input, "first volume");

            var result = ArchiveInputNormalizer.Prepare(input);

            Assert.False(result.WasRenamed);
            Assert.Equal(input, result.ArchivePath);
            Assert.True(File.Exists(input));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DoesNotOverwriteExistingNormalizedTarget()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var input = Path.Combine(directory, "duplicate.mp4");
            var target = Path.Combine(directory, "duplicate.zip");
            File.WriteAllText(input, "source");
            File.WriteAllText(target, "existing");

            var exception = Assert.Throws<IOException>(() => ArchiveInputNormalizer.Prepare(input));

            Assert.Contains("目标文件已存在", exception.Message, StringComparison.Ordinal);
            Assert.Equal("source", File.ReadAllText(input));
            Assert.Equal("existing", File.ReadAllText(target));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"HDecompressHelper.Normalize-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
