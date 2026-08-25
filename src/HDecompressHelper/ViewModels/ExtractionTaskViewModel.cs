using CommunityToolkit.Mvvm.ComponentModel;
using HDecompressHelper.Models;

namespace HDecompressHelper.ViewModels;

public sealed partial class ExtractionTaskViewModel : ObservableObject
{
    public ExtractionTaskViewModel(
        string archivePath,
        string destinationPath,
        ExtractionEngine engine,
        string password)
    {
        Id = Guid.NewGuid();
        ArchivePath = Path.GetFullPath(archivePath);
        DestinationPath = Path.GetFullPath(destinationPath);
        ArchiveName = Path.GetFileName(ArchivePath);
        ArchiveSize = File.Exists(ArchivePath) ? new FileInfo(ArchivePath).Length : 0;
        Engine = engine;
        Password = password;
    }

    public ExtractionTaskViewModel(ExtractionHistoryRecord history)
    {
        Id = history.Id == Guid.Empty ? Guid.NewGuid() : history.Id;
        ArchivePath = history.ArchivePath;
        DestinationPath = history.DestinationPath;
        ArchiveName = string.IsNullOrWhiteSpace(history.ArchiveName)
            ? Path.GetFileName(history.ArchivePath)
            : history.ArchiveName;
        ArchiveSize = history.ArchiveSize;
        Engine = history.Engine;
        Password = history.Password;
        Status = history.Status is ExtractionStatus.Queued or ExtractionStatus.Extracting or ExtractionStatus.Cancelling
            ? ExtractionStatus.Interrupted
            : history.Status;
        Progress = history.Progress;
        CurrentFile = history.CurrentFile;
        StartTime = history.StartTime;
        EndTime = history.EndTime;
        ErrorMessage = history.ErrorMessage;
        DetailText = history.DetailText;
    }

    public Guid Id { get; }

    public string ArchivePath { get; }

    public string DestinationPath { get; }

    public string ArchiveName { get; }

    public long ArchiveSize { get; }

    public ExtractionEngine Engine { get; }

    public string Password { get; private set; }

    public string EngineLabel => Engine == ExtractionEngine.SevenZip ? "7-Zip" : "WinRAR";

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    public void SetPassword(string password)
    {
        password ??= string.Empty;
        if (string.Equals(Password, password, StringComparison.Ordinal))
        {
            return;
        }

        Password = password;
        OnPropertyChanged(nameof(Password));
        OnPropertyChanged(nameof(HasPassword));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    [NotifyPropertyChangedFor(nameof(StatusDescription))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    private ExtractionStatus _status = ExtractionStatus.Queued;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFileDisplay))]
    private string? _currentFile;

    [ObservableProperty]
    private DateTimeOffset? _startTime;

    [ObservableProperty]
    private DateTimeOffset? _endTime;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private int? _processId;

    public string ArchiveSizeText => FormatBytes(ArchiveSize);

    public string StatusLabel => Status switch
    {
        ExtractionStatus.Queued => "等待中",
        ExtractionStatus.Extracting => "解压中",
        ExtractionStatus.Cancelling => "正在取消",
        ExtractionStatus.Completed => "已完成",
        ExtractionStatus.CompletedWithWarning => "已完成 · 警告",
        ExtractionStatus.Failed => "失败",
        ExtractionStatus.Cancelled => "已取消",
        ExtractionStatus.Interrupted => "已中断",
        _ => Status.ToString()
    };

    public string StatusDescription => Status switch
    {
        ExtractionStatus.Queued => $"等待可用的 {EngineLabel} 工作线程…",
        ExtractionStatus.Extracting => ProcessId is { } id
            ? $"{EngineLabel} 进程 #{id} 正在运行"
            : $"正在启动 {EngineLabel}…",
        ExtractionStatus.Cancelling => $"正在停止对应的 {EngineLabel} 进程…",
        ExtractionStatus.Completed => $"完成于 {EndTime:HH:mm:ss}",
        ExtractionStatus.CompletedWithWarning => $"任务完成，请查看 {EngineLabel} 警告",
        ExtractionStatus.Failed => ErrorMessage ?? "解压任务失败",
        ExtractionStatus.Cancelled => "任务已由用户取消",
        ExtractionStatus.Interrupted => "上次运行意外中断",
        _ => string.Empty
    };

    public string CurrentFileDisplay => string.IsNullOrWhiteSpace(CurrentFile)
        ? Status is ExtractionStatus.Extracting or ExtractionStatus.Cancelling
            ? "正在读取归档信息…"
            : "尚未开始处理文件"
        : CurrentFile;

    public string ElapsedText
    {
        get
        {
            if (StartTime is null)
            {
                return "00:00:00";
            }

            var end = EndTime ?? DateTimeOffset.Now;
            var elapsed = end - StartTime.Value;
            return elapsed.TotalHours >= 100
                ? $"{(int)elapsed.TotalHours:000}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
                : $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }
    }

    public bool CanCancel => Status is ExtractionStatus.Queued or ExtractionStatus.Extracting;

    public bool CanRemove => Status is
        ExtractionStatus.Completed or
        ExtractionStatus.CompletedWithWarning or
        ExtractionStatus.Failed or
        ExtractionStatus.Cancelled or
        ExtractionStatus.Interrupted;

    public ExtractionHistoryRecord ToHistoryRecord() => new()
    {
        Id = Id,
        ArchivePath = ArchivePath,
        DestinationPath = DestinationPath,
        ArchiveName = ArchiveName,
        ArchiveSize = ArchiveSize,
        Engine = Engine,
        Status = Status,
        Progress = Progress,
        CurrentFile = CurrentFile,
        StartTime = StartTime,
        EndTime = EndTime,
        ErrorMessage = ErrorMessage,
        DetailText = DetailText,
        Password = Password
    };

    public void ApplyProgress(ExtractionProgress update)
    {
        Progress = Math.Clamp(update.Percent, 0, 100);
        if (!string.IsNullOrWhiteSpace(update.CurrentFile))
        {
            CurrentFile = update.CurrentFile;
        }
    }

    public void MarkWorkerStarted()
    {
        StartTime ??= DateTimeOffset.Now;
        Status = ExtractionStatus.Extracting;
        OnPropertyChanged(nameof(ElapsedText));
    }

    public void MarkEnded()
    {
        EndTime ??= DateTimeOffset.Now;
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(StatusDescription));
    }

    public void RefreshElapsed() => OnPropertyChanged(nameof(ElapsedText));

    partial void OnStartTimeChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(ElapsedText));

    partial void OnEndTimeChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(StatusDescription));
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(StatusDescription));

    partial void OnProcessIdChanged(int? value) => OnPropertyChanged(nameof(StatusDescription));

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
