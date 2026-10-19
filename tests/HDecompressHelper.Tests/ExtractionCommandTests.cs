using HDecompressHelper.Services;

namespace HDecompressHelper.Tests;

public sealed class ExtractionCommandTests
{
    [Fact]
    public void SevenZipArgumentsAndLogContainPlaintextPassword()
    {
        const string password = "7z pass & symbols";
        var startInfo = SevenZipExtractionService.CreateStartInfo(
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Archives\demo file.7z",
            @"C:\Output Folder",
            password);
        var command = SevenZipExtractionService.BuildDisplayCommand(
            startInfo.FileName,
            @"C:\Archives\demo file.7z",
            @"C:\Output Folder",
            password);

        Assert.Contains($"-p{password}", startInfo.ArgumentList);
        Assert.Contains(password, command, StringComparison.Ordinal);
        Assert.Contains("-bsp1", startInfo.ArgumentList);
        Assert.True(startInfo.RedirectStandardInput);
    }

    [Fact]
    public void WinRarArgumentsAndLogContainPlaintextPassword()
    {
        const string password = "rar pass & symbols";
        var startInfo = WinRarExtractionService.CreateStartInfo(
            @"C:\Program Files\WinRAR\WinRAR.exe",
            @"C:\Archives\demo file.rar",
            @"C:\Output Folder",
            password);
        var command = WinRarExtractionService.BuildDisplayCommand(
            startInfo.FileName,
            @"C:\Archives\demo file.rar",
            @"C:\Output Folder",
            password);

        Assert.Equal("x", startInfo.ArgumentList[0]);
        Assert.Contains("-o+", startInfo.ArgumentList);
        Assert.Contains("-y", startInfo.ArgumentList);
        Assert.Contains("-ibck", startInfo.ArgumentList);
        Assert.Contains($"-p{password}", startInfo.ArgumentList);
        Assert.Contains(password, command, StringComparison.Ordinal);
        Assert.Contains("-ibck", command, StringComparison.Ordinal);
        Assert.True(startInfo.RedirectStandardInput);
    }

    [Fact]
    public void WinRarWithoutPasswordDisablesInteractivePasswordPrompt()
    {
        var startInfo = WinRarExtractionService.CreateStartInfo(
            @"C:\Program Files\WinRAR\WinRAR.exe",
            @"C:\Archives\protected.rar",
            @"C:\Output",
            password: string.Empty);
        var command = WinRarExtractionService.BuildDisplayCommand(
            startInfo.FileName,
            @"C:\Archives\protected.rar",
            @"C:\Output",
            password: string.Empty);

        Assert.Contains("-p-", startInfo.ArgumentList);
        Assert.Contains("-p-", command, StringComparison.Ordinal);
        Assert.True(startInfo.RedirectStandardInput);
    }
}
