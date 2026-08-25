using System.Text;

namespace HDecompressHelper.Infrastructure;

internal sealed class CappedLogBuffer(int capacity)
{
    private readonly StringBuilder _buffer = new(Math.Min(capacity, 16 * 1024));
    private bool _isTruncated;

    public void Append(string value) => Append(value.AsSpan());

    public void Append(ReadOnlySpan<char> value)
    {
        var remaining = capacity - _buffer.Length;
        if (remaining <= 0)
        {
            _isTruncated = true;
            return;
        }

        var count = Math.Min(remaining, value.Length);
        _buffer.Append(value[..count]);
        _isTruncated |= count < value.Length;
    }

    public override string ToString() => _isTruncated
        ? $"{_buffer}{Environment.NewLine}… 日志已截断（上限 {capacity:N0} 字符）"
        : _buffer.ToString();
}
