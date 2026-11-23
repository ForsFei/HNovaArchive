using System.Text.RegularExpressions;
using HDecompressHelper.Infrastructure;

namespace HDecompressHelper.Services;

// Called by one background scan at a time; state is independent of the visible history limit.
internal sealed class ArchiveWorkflowScanner
{
    private readonly string _statePath;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _processed;
    private readonly Dictionary<string, (string Stamp, DateTimeOffset Since)> _observed = new(StringComparer.OrdinalIgnoreCase);
    public string LastScanSummary { get; private set; } = "尚未扫描";

    public ArchiveWorkflowScanner(string? statePath = null)
    {
        _statePath = statePath ?? Path.Combine(JsonFileStorage.GetDefaultDataDirectory(), "workflow.json");
        _processed = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(
            JsonFileStorage.Load(_statePath, () => new Dictionary<string, string>()), StringComparer.OrdinalIgnoreCase);
    }

    public static string Stamp(string path)
    {
        var info = new FileInfo(path);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    public void MarkProcessed(string path, string stamp)
    {
        _processed[Path.GetFullPath(path)] = stamp;
        JsonFileStorage.Save(_statePath, _processed);
    }

    public bool IsUnchangedSinceScan(string path) =>
        _observed.TryGetValue(path, out var observed) && observed.Stamp == Stamp(path);

    public IReadOnlyList<string> Scan(IEnumerable<string> roots, IEnumerable<string> blockedDirectories,
        DateTimeOffset now, CancellationToken token)
    {
        var blocked = blockedDirectories.Select(Path.GetFullPath).ToArray();
        var candidates = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var waiting = 0;
        var processed = 0;
        var errors = new List<string>();
        foreach (var root in roots)
        {
            var pending = new Stack<(string Path, int Depth)>();
            pending.Push((Path.GetFullPath(root), 0));
            while (pending.TryPop(out var item))
            {
                token.ThrowIfCancellationRequested();
                if (item.Depth > 16 || !visited.Add(item.Path) || blocked.Any(b => IsWithin(item.Path, b))) continue;
                try
                {
                    if ((File.GetAttributes(item.Path) & FileAttributes.ReparsePoint) != 0) continue;
                    var files = Directory.GetFiles(item.Path);
                    var stamps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var path in files)
                    {
                        try
                        {
                            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                            seenFiles.Add(path);
                            var stamp = Stamp(path);
                            stamps[path] = stamp;
                            if (!_observed.TryGetValue(path, out var previous) || previous.Stamp != stamp)
                            {
                                _observed[path] = (stamp, now);
                            }
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        { errors.Add($"{Path.GetFileName(path)}：{ex.Message}"); }
                    }
                    foreach (var pair in stamps)
                    {
                        if (_processed.TryGetValue(pair.Key, out var done) && done == pair.Value) { processed++; continue; }
                        if (now - _observed[pair.Key].Since < TimeSpan.FromSeconds(5)) { waiting++; continue; }
                        // Split archives need all nearby volumes to settle, but an unrelated
                        // changing file must not hold back an ordinary ZIP in this directory.
                        var split = Regex.IsMatch(pair.Key, @"\.(7z|zip|rar)\.001$|\.part0*1\.rar$", RegexOptions.IgnoreCase);
                        if (split && stamps.Any(p => now - _observed[p.Key].Since < TimeSpan.FromSeconds(5))) { waiting++; continue; }
                        try { if (IsCandidate(pair.Key)) candidates.Add(pair.Key); }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        { errors.Add($"{Path.GetFileName(pair.Key)}：{ex.Message}"); }
                    }
                    foreach (var child in Directory.GetDirectories(item.Path)) pending.Push((child, item.Depth + 1));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { errors.Add($"{item.Path}：{ex.Message}"); }
            }
        }
        foreach (var stale in _observed.Keys.Where(p => !seenFiles.Contains(p)).ToArray()) _observed.Remove(stale);
        var result = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        LastScanSummary = $"扫描 {seenFiles.Count} 个文件 · 候选 {result.Length} · 等待稳定 {waiting} · 已处理 {processed}";
        if (errors.Count > 0) LastScanSummary += $" · 读取失败 {errors.Count}：{errors[0]}";
        return result;
    }

    internal static bool IsWithin(string path, string directory) =>
        path.Equals(directory, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    internal static bool IsCandidate(string path)
    {
        var name = Path.GetFileName(path);
        // Non-first split volumes are data segments, never independent archives.
        if (Regex.IsMatch(name, @"\.(7z|zip|rar)\.\d+$", RegexOptions.IgnoreCase) &&
            !name.EndsWith(".001", StringComparison.OrdinalIgnoreCase))
        {
            // .11111 is a disguised suffix, while .002, .003 etc. are real volume numbers.
            if (Regex.IsMatch(name, @"\.\d{3}$")) return false;
        }
        if (Regex.IsMatch(name, @"\.(r\d\d|z\d\d)$", RegexOptions.IgnoreCase)) return false;
        var part = Regex.Match(name, @"\.part(\d+)\.rar$", RegexOptions.IgnoreCase);
        if (part.Success && (!int.TryParse(part.Groups[1].Value, out var number) || number != 1)) return false;
        if (ArchiveInputNormalizer.IsLoadableArchivePath(path)) return true;
        if (Regex.IsMatch(name, @"\.(zip|7z|rar|tar|gz|xz|bz2)\..+$", RegexOptions.IgnoreCase)) return true;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> header = stackalloc byte[512];
        var count = stream.Read(header);
        var bytes = header[..count];
        return bytes.StartsWith(new byte[] { 0x50, 0x4b, 3, 4 }) ||
               bytes.StartsWith(new byte[] { 0x50, 0x4b, 5, 6 }) ||
               bytes.StartsWith(new byte[] { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c }) ||
               bytes.StartsWith("Rar!"u8) || bytes.StartsWith(new byte[] { 0x1f, 0x8b }) ||
               bytes.StartsWith("BZh"u8) || bytes.StartsWith("MSCF"u8) ||
               bytes.StartsWith(new byte[] { 0xfd, 0x37, 0x7a, 0x58, 0x5a, 0 }) ||
               (count >= 262 && bytes[257..262].SequenceEqual("ustar"u8));
    }
}
