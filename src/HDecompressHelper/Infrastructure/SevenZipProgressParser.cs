using System.Text;
using System.Text.RegularExpressions;
using HDecompressHelper.Models;

namespace HDecompressHelper.Infrastructure;

/// <summary>
/// Incrementally parses 7-Zip's console stream. Both CR and LF are record
/// delimiters because 7-Zip refreshes progress in-place with carriage returns.
/// </summary>
public sealed partial class SevenZipProgressParser : IProgressStreamParser
{
    private readonly StringBuilder _pending = new();
    private int? _lastPercent;
    private string? _currentFile;

    public IReadOnlyList<ExtractionProgress> Append(string chunk) =>
        Append(chunk.AsSpan());

    public IReadOnlyList<ExtractionProgress> Append(ReadOnlySpan<char> chunk)
    {
        var updates = new List<ExtractionProgress>();

        foreach (var character in chunk)
        {
            if (character is '\r' or '\n')
            {
                ParseRecord(_pending.ToString(), updates);
                _pending.Clear();
                continue;
            }

            _pending.Append(character);
        }

        return updates;
    }

    public IReadOnlyList<ExtractionProgress> Complete()
    {
        var updates = new List<ExtractionProgress>();
        ParseRecord(_pending.ToString(), updates);
        _pending.Clear();
        return updates;
    }

    private void ParseRecord(string record, ICollection<ExtractionProgress> updates)
    {
        if (string.IsNullOrWhiteSpace(record))
        {
            return;
        }

        var progressMatch = ProgressRecordRegex().Match(record);
        if (progressMatch.Success &&
            long.TryParse(progressMatch.Groups["percent"].Value, out var rawPercent))
        {
            _lastPercent = (int)Math.Clamp(rawPercent, 0, 100);

            var tail = progressMatch.Groups["tail"].Value.Trim();
            if (!string.IsNullOrEmpty(tail) && !long.TryParse(tail, out _))
            {
                _currentFile = tail;
            }

            updates.Add(new ExtractionProgress(_lastPercent.Value, _currentFile));
            return;
        }

        var fileMatch = FileRecordRegex().Match(record);
        if (!fileMatch.Success)
        {
            return;
        }

        _currentFile = fileMatch.Groups["file"].Value.Trim();
        if (_lastPercent.HasValue)
        {
            updates.Add(new ExtractionProgress(_lastPercent.Value, _currentFile));
        }
    }

    [GeneratedRegex(@"^\s*(?<percent>[+-]?\d+)\s*%(?:\s+(?<tail>.*?))?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProgressRecordRegex();

    [GeneratedRegex(@"^\s*-\s+(?<file>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FileRecordRegex();
}
