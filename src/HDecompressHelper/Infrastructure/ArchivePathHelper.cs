namespace HDecompressHelper.Infrastructure;

public static class ArchivePathHelper
{
    private static readonly string[] CompoundExtensions =
    [
        ".tar.gz",
        ".tar.gzip",
        ".tar.xz",
        ".tar.bz2",
        ".7z.001",
        ".zip.001",
        ".rar.001"
    ];

    public static string GetDefaultDestination(string archivePath)
    {
        var fullPath = Path.GetFullPath(archivePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("压缩文件路径没有有效目录。", nameof(archivePath));
        var fileName = Path.GetFileName(fullPath);

        var baseName = CompoundExtensions
            .FirstOrDefault(extension => fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) is { } compound
            ? fileName[..^compound.Length]
            : Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "Extracted";
        }

        return Path.Combine(directory, baseName);
    }
}
