using HDecompressHelper.Models;
using HDecompressHelper.ViewModels;

namespace HDecompressHelper.Tests;

public sealed class ExtractionTaskViewModelTests
{
    [Theory]
    [InlineData(ExtractionStatus.Queued)]
    [InlineData(ExtractionStatus.Extracting)]
    [InlineData(ExtractionStatus.Cancelling)]
    public void ActiveHistoryIsRestoredAsInterrupted(ExtractionStatus savedStatus)
    {
        var record = new ExtractionHistoryRecord
        {
            Id = Guid.NewGuid(),
            ArchivePath = @"C:\Archives\resume.rar",
            DestinationPath = @"C:\Archives\resume",
            ArchiveName = "resume.rar",
            Engine = ExtractionEngine.WinRar,
            Status = savedStatus,
            Password = "persisted-plaintext",
            DetailText = "persisted log"
        };

        var task = new ExtractionTaskViewModel(record);
        var roundTrip = task.ToHistoryRecord();

        Assert.Equal(ExtractionStatus.Interrupted, task.Status);
        Assert.Equal(ExtractionEngine.WinRar, task.Engine);
        Assert.Equal("persisted-plaintext", task.Password);
        Assert.True(task.HasPassword);
        Assert.Equal("persisted-plaintext", roundTrip.Password);
        Assert.Equal("persisted log", roundTrip.DetailText);
    }
}
