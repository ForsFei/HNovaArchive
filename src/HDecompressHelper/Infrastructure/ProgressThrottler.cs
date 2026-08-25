using System.Diagnostics;
using HDecompressHelper.Models;

namespace HDecompressHelper.Infrastructure;

internal sealed class ProgressThrottler(
    IProgress<ExtractionProgress>? target,
    TimeSpan minimumInterval)
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _lastSent;
    private bool _hasSent;
    private ExtractionProgress? _latest;

    public void Report(ExtractionProgress value)
    {
        if (target is null)
        {
            return;
        }

        _latest = value;
        if (!_hasSent || value.Percent >= 100 || _stopwatch.Elapsed - _lastSent >= minimumInterval)
        {
            SendLatest();
        }
    }

    public void Flush() => SendLatest();

    private void SendLatest()
    {
        if (_latest is null || target is null)
        {
            return;
        }

        target.Report(_latest);
        _latest = null;
        _lastSent = _stopwatch.Elapsed;
        _hasSent = true;
    }
}
