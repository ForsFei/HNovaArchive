using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;

namespace HDecompressHelper.Tests;

public sealed class SevenZipExitCodeMapperTests
{
    [Theory]
    [InlineData(0, ExtractionExitKind.Success)]
    [InlineData(1, ExtractionExitKind.Warning)]
    [InlineData(2, ExtractionExitKind.FatalError)]
    [InlineData(7, ExtractionExitKind.CommandLineError)]
    [InlineData(8, ExtractionExitKind.OutOfMemory)]
    [InlineData(255, ExtractionExitKind.Stopped)]
    [InlineData(42, ExtractionExitKind.UnknownError)]
    public void MapsDocumentedAndUnknownExitCodes(int code, ExtractionExitKind expected)
    {
        var result = SevenZipExitCodeMapper.Map(code);

        Assert.Equal(expected, result.Kind);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
    }
}
