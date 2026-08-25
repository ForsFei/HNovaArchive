using System.Text;
using System.Text.RegularExpressions;
using HDecompressHelper.Models;

namespace HDecompressHelper.Infrastructure;

/// <summary>
/// Best-effort parser for the textual progress emitted by console RAR/UnRAR.
/// WinRAR does not expose a stable machine-readable progress protocol.
/// </summary>
public sealed partial class WinRarProgressParser : IProgressStreamParser
{
    private readonly StringBuilder _pending = new();
    private int? _lastPercent;
    private string? _currentFile;

    public IReadOnlyList<ExtractionProgress> Append(string chunk)
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

            if (!char.IsControl(character) || character == '\t')
            {
                _pending.Append(character);
            }
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

        var fileMatch = CurrentFileRegex().Match(record);
        if (fileMatch.Success)
        {
            _currentFile = fileMatch.Groups["file"].Value.Trim();
        }

        var progressMatch = PercentRegex().Match(record);
        if (progressMatch.Success && int.TryParse(progressMatch.Groups["percent"].Value, out var percent))
        {
            _lastPercent = Math.Clamp(percent, 0, 100);
            updates.Add(new ExtractionProgress(_lastPercent.Value, _currentFile));
            return;
        }

        if (fileMatch.Success && _lastPercent.HasValue)
        {
            updates.Add(new ExtractionProgress(_lastPercent.Value, _currentFile));
        }
    }

    [GeneratedRegex(@"(?<!\d)(?<percent>\d{1,3})\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentRegex();

    [GeneratedRegex(@"^\s*(?:Extracting|Testing)\s+(?<file>.+?)(?:\s+(?:\d{1,3}\s*%|OK))?\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CurrentFileRegex();
}

