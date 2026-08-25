using System.Text.RegularExpressions;

namespace HDecompressHelper.Infrastructure;

public sealed record ArchiveInputPreparation(
    string OriginalPath,
    string ArchivePath,
    bool WasRenamed);

public static class ArchiveInputNormalizer
{
    private static readonly string[] KnownExtensions =
    [
        ".gzip", ".tbz2", ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz",
        ".xz", ".txz", ".bz2", ".cab", ".iso", ".wim"
    ];

    private static readonly HashSet<string> KnownExtensionSet =
        new(KnownExtensions, StringComparer.OrdinalIgnoreCase);

    private static readonly Regex SplitArchiveRegex = new(
        @"\.(?:7z|zip|rar)\.001$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsLoadableArchivePath(string path)
    {
        var fileName = Path.GetFileName(path);
        return SplitArchiveRegex.IsMatch(fileName) ||
               KnownExtensionSet.Contains(Path.GetExtension(fileName));
    }

    public static ArchiveInputPreparation Prepare(string path)
    {
        var originalPath = Path.GetFullPath(path);
        if (!File.Exists(originalPath))
        {
            throw new FileNotFoundException("找不到要加载的文件。", originalPath);
        }

        if (IsLoadableArchivePath(originalPath))
        {
            return new ArchiveInputPreparation(originalPath, originalPath, WasRenamed: false);
        }

        var archivePath = GetNormalizedPath(originalPath);
        if (File.Exists(archivePath))
        {
            throw new IOException($"无法自动重命名：目标文件已存在。{Environment.NewLine}{archivePath}");
        }

        File.Move(originalPath, archivePath);
        return new ArchiveInputPreparation(originalPath, archivePath, WasRenamed: true);
    }

    internal static string GetNormalizedPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("文件路径没有有效目录。", nameof(path));
        var fileName = Path.GetFileName(fullPath);
        var knownArchiveEnd = FindKnownArchiveEnd(fileName);

        var normalizedName = knownArchiveEnd > 0
            ? fileName[..knownArchiveEnd]
            : Path.GetFileNameWithoutExtension(fileName) + ".zip";
        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(normalizedName)))
        {
            normalizedName = "Archive.zip";
        }

        return Path.Combine(directory, normalizedName);
    }

    private static int FindKnownArchiveEnd(string fileName)
    {
        var bestEnd = -1;
        foreach (var extension in KnownExtensions)
        {
            var index = fileName.LastIndexOf(
                extension + ".",
                StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                bestEnd = Math.Max(bestEnd, index + extension.Length);
            }
        }

        return bestEnd;
    }
}
