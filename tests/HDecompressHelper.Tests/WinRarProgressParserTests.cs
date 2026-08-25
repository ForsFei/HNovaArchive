using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;

namespace HDecompressHelper.Tests;

public sealed class WinRarProgressParserTests
{
    [Fact]
    public void ParsesSplitCarriageReturnProgressAndCurrentFile()
    {
        var parser = new WinRarProgressParser();

        var first = parser.Append("Extracting  docs\\中文 fi");
        var second = parser.Append("le.txt\r 42%\rTesting  next.bin  78%\r");

        Assert.Empty(first);
        Assert.Contains(second, update =>
            update.Percent == 42 && update.CurrentFile == "docs\\中文 file.txt");
        Assert.Contains(second, update =>
            update.Percent == 78 && update.CurrentFile == "next.bin");
    }

    [Theory]
    [InlineData(0, ExtractionExitKind.Success)]
    [InlineData(1, ExtractionExitKind.Warning)]
    [InlineData(5, ExtractionExitKind.WriteError)]
    [InlineData(11, ExtractionExitKind.FatalError)]
    [InlineData(12, ExtractionExitKind.ReadError)]
    [InlineData(255, ExtractionExitKind.Stopped)]
    [InlineData(42, ExtractionExitKind.UnknownError)]
    public void MapsWinRarExitCodes(int code, ExtractionExitKind expected)
    {
        var result = WinRarExitCodeMapper.Map(code);

        Assert.Equal(expected, result.Kind);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
    }
}
