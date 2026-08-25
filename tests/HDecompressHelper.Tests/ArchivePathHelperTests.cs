using HDecompressHelper.Infrastructure;

namespace HDecompressHelper.Tests;

public sealed class ArchivePathHelperTests
{
    [Theory]
    [InlineData(@"C:\Downloads\demo.zip", @"C:\Downloads\demo")]
    [InlineData(@"C:\Downloads\backup.tar.gz", @"C:\Downloads\backup")]
    [InlineData(@"C:\Downloads\backup.TAR.XZ", @"C:\Downloads\backup")]
    [InlineData(@"C:\Downloads\111.7z.001", @"C:\Downloads\111")]
    public void CreatesExpectedDefaultDestination(string archive, string expected)
    {
        Assert.Equal(expected, ArchivePathHelper.GetDefaultDestination(archive));
    }
}
