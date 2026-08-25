using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;

namespace HDecompressHelper.Tests;

public sealed class SevenZipProgressParserTests
{
    [Theory]
    [InlineData("  0%", 0)]
    [InlineData(" 10%", 10)]
    [InlineData("100%", 100)]
    [InlineData("120%", 100)]
    [InlineData(" -5%", 0)]
    public void ParsesAndClampsPercent(string input, int expected)
    {
        var update = ParseAll(input).Single();

        Assert.Equal(expected, update.Percent);
    }

    [Theory]
    [InlineData(" 35% file.txt", "file.txt")]
    [InlineData(" 35% file name with spaces.txt", "file name with spaces.txt")]
    [InlineData(" 35% 中文 文件名.txt", "中文 文件名.txt")]
    public void ParsesInlineCurrentFile(string input, string expectedFile)
    {
        var update = ParseAll(input).Single();

        Assert.Equal(35, update.Percent);
        Assert.Equal(expectedFile, update.CurrentFile);
    }

    [Fact]
    public void TreatsCarriageReturnAsARecordDelimiter()
    {
        var updates = ParseAll("\r 12%\r 13%\r 14%");

        Assert.Equal([12, 13, 14], updates.Select(update => update.Percent));
    }

    [Fact]
    public void ParsesBb1FileRecordSeparatelyFromProgress()
    {
        var updates = ParseAll(" 35% 10\r      \r- 中文 文件名.txt\r\n");

        Assert.Equal(2, updates.Count);
        Assert.Null(updates[0].CurrentFile);
        Assert.Equal("中文 文件名.txt", updates[1].CurrentFile);
    }

    [Fact]
    public void ReassemblesRecordsSplitAcrossBuffers()
    {
        var parser = new SevenZipProgressParser();

        Assert.Empty(parser.Append(" 3"));
        Assert.Empty(parser.Append("5% abc.tx"));
        var updates = parser.Append("t\r");

        var update = Assert.Single(updates);
        Assert.Equal(new ExtractionProgress(35, "abc.txt"), update);
    }

    [Fact]
    public void IgnoresUnknownConsoleOutput()
    {
        var updates = ParseAll("Scanning the drive\r\nEverything is Ok\r\n");

        Assert.Empty(updates);
    }

    private static IReadOnlyList<ExtractionProgress> ParseAll(string input)
    {
        var parser = new SevenZipProgressParser();
        var updates = new List<ExtractionProgress>();
        updates.AddRange(parser.Append(input));
        updates.AddRange(parser.Complete());
        return updates;
    }
}

