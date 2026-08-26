using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;
using HDecompressHelper.Services;

namespace HDecompressHelper.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MaxHistoryRecords = 100;
    private const int MaxSavedPasswords = 50;

    private readonly IExtractionQueueService _queueService;
    private readonly IPasswordRetryService _passwordRetryService;
    private readonly IFileExplorerService _fileExplorerService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ISevenZipLocator _sevenZipLocator;
    private readonly IWinRarLocator _winRarLocator;
    private readonly ISettingsService _settingsService;
    private readonly IHistoryService _historyService;
    private readonly IThemeService _themeService;
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellations = [];
    private readonly DispatcherTimer _elapsedTimer;
    private readonly DispatcherTimer _settingsSaveTimer;
    private bool _isInitializing = true;
    private bool _disposed;

    public MainViewModel(
        IExtractionQueueService queueService,
        IPasswordRetryService passwordRetryService,
        IFileExplorerService fileExplorerService,
        IFileDialogService fileDialogService,
        ISevenZipLocator sevenZipLocator,
        IWinRarLocator winRarLocator,
        ISettingsService settingsService,
        IHistoryService historyService,
        IThemeService themeService)
    {
        _queueService = queueService;
        _passwordRetryService = passwordRetryService;
        _fileExplorerService = fileExplorerService;
        _fileDialogService = fileDialogService;
        _sevenZipLocator = sevenZipLocator;
        _winRarLocator = winRarLocator;
        _settingsService = settingsService;
        _historyService = historyService;
        _themeService = themeService;

        _elapsedTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            OnTimerTick,
            Dispatcher.CurrentDispatcher);
        _settingsSaveTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(400),
            DispatcherPriority.Background,
            OnSettingsSaveTimerTick,
            Dispatcher.CurrentDispatcher);
        _settingsSaveTimer.Stop();

        LoadSettings();
        RestoreHistory();
        _isInitializing = false;
        RefreshEngineStatus();
    }

    public ObservableCollection<ExtractionTaskViewModel> Tasks { get; } = [];

    public ObservableCollection<string> SavedPasswords { get; } = [];

    public int MaxConcurrentExtractions => _queueService.MaxConcurrentExtractions;

    public int ActiveCount => Tasks.Count(task => task.Status is
        ExtractionStatus.Queued or ExtractionStatus.Extracting or ExtractionStatus.Cancelling);

    public int CompletedCount => Tasks.Count(task => task.Status is
        ExtractionStatus.Completed or ExtractionStatus.CompletedWithWarning);

    public bool IsListView => ViewMode == TaskViewMode.List;

    public bool IsTilesView => ViewMode == TaskViewMode.Tiles;

    public bool IsSevenZipSelected => SelectedEngine == ExtractionEngine.SevenZip;

    public bool IsWinRarSelected => SelectedEngine == ExtractionEngine.WinRar;

    public string ChooseEngineText => SelectedEngine == ExtractionEngine.SevenZip
        ? "选择 7-Zip"
        : "选择 WinRAR";

    public string ThemeToggleText => Theme == AppTheme.Dark ? "浅色背景" : "深色背景";

    [ObservableProperty]
    private string _statusMessage = "拖放压缩文件到窗口，或点击右上角添加任务。";

    [ObservableProperty]
    private string _engineStatusText = string.Empty;

    [ObservableProperty]
    private bool _isSelectedEngineAvailable;

    [ObservableProperty]
    private ExtractionEngine _selectedEngine = ExtractionEngine.SevenZip;

    [ObservableProperty]
    private AppTheme _theme = AppTheme.Dark;

    [ObservableProperty]
    private TaskViewMode _viewMode = TaskViewMode.List;

    [ObservableProperty]
    private string _newTaskPassword = string.Empty;

    [ObservableProperty]
    private bool _isDetailsOpen;

    [ObservableProperty]
    private string _detailsTitle = string.Empty;

    [ObservableProperty]
    private string _detailsText = string.Empty;

    [RelayCommand]
    private void AddArchivesFromDialog() => AddArchives(_fileDialogService.PickArchives());

    public void AddArchives(IEnumerable<string> archivePaths)
    {
        RefreshEngineStatus();
        if (!IsSelectedEngineAvailable)
        {
            StatusMessage = SelectedEngine == ExtractionEngine.SevenZip
                ? "尚未找到 7z.exe，请先点击“选择 7-Zip”。"
                : "尚未找到 WinRAR 命令行程序，请先点击“选择 WinRAR”。";
            return;
        }

        var added = 0;
        var renamed = 0;
        var errors = new List<string>();
        var password = NewTaskPassword;
        foreach (var candidate in archivePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ArchiveInputPreparation preparation;
            try
            {
                preparation = ArchiveInputNormalizer.Prepare(candidate);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                errors.Add($"{Path.GetFileName(candidate)}：{exception.Message}");
                continue;
            }

            var fullPath = preparation.ArchivePath;
            if (Tasks.Any(task =>
                    string.Equals(task.ArchivePath, fullPath, StringComparison.OrdinalIgnoreCase) &&
                    task.Status is ExtractionStatus.Queued or ExtractionStatus.Extracting or ExtractionStatus.Cancelling))
            {
                continue;
            }

            if (preparation.WasRenamed)
            {
                renamed++;
            }

            var task = new ExtractionTaskViewModel(
                fullPath,
                ArchivePathHelper.GetDefaultDestination(fullPath),
                SelectedEngine,
                password);
            task.PropertyChanged += OnTaskPropertyChanged;
            Tasks.Insert(0, task);
            StartTask(task);
            added++;
        }

        var addedMessage = added switch
        {
            0 => errors.Count == 0 ? "没有发现可添加的文件。" : "没有文件成功加入任务。",
            1 => "已添加 1 个解压任务。",
            _ => $"已添加 {added} 个解压任务，最多并行 {_queueService.MaxConcurrentExtractions} 个。"
        };
        var renamedMessage = renamed == 0 ? string.Empty : $" 已自动重命名 {renamed} 个文件。";
        var errorMessage = errors.Count == 0
            ? string.Empty
            : $" {errors.Count} 个文件处理失败：{errors[0]}";
        StatusMessage = addedMessage + renamedMessage + errorMessage;
        NotifySummaryChanged();
    }

    [RelayCommand]
    private void ChooseEngineExecutable()
    {
        var path = _fileDialogService.PickExtractionExecutable(SelectedEngine);
        if (path is null)
        {
            return;
        }

        var accepted = SelectedEngine == ExtractionEngine.SevenZip
            ? _sevenZipLocator.TryUse(path)
            : _winRarLocator.TryUse(path);
        if (!accepted)
        {
            StatusMessage = SelectedEngine == ExtractionEngine.SevenZip
                ? "选择的文件不是有效的 7-Zip 命令行程序。"
                : "请选择 Rar.exe、UnRAR.exe 或 WinRAR.exe。";
            return;
        }

        RefreshEngineStatus();
        SaveSettingsNow();
        StatusMessage = $"{(SelectedEngine == ExtractionEngine.SevenZip ? "7-Zip" : "WinRAR")} 路径已更新。";
    }

    [RelayCommand]
    private void SelectSevenZip() => SelectedEngine = ExtractionEngine.SevenZip;

    [RelayCommand]
    private void SelectWinRar() => SelectedEngine = ExtractionEngine.WinRar;

    [RelayCommand]
    private void SetListView() => ViewMode = TaskViewMode.List;

    [RelayCommand]
    private void SetTilesView() => ViewMode = TaskViewMode.Tiles;

    [RelayCommand]
    private void ToggleTheme() => Theme = Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;

    [RelayCommand(CanExecute = nameof(CanDeleteSavedPassword))]
    private void DeleteSavedPassword()
    {
        var savedPassword = SavedPasswords.FirstOrDefault(password =>
            string.Equals(password, NewTaskPassword, StringComparison.Ordinal));
        if (savedPassword is null)
        {
            return;
        }

        SavedPasswords.Remove(savedPassword);
        NewTaskPassword = SavedPasswords.FirstOrDefault() ?? string.Empty;
        SaveSettingsNow();
        DeleteSavedPasswordCommand.NotifyCanExecuteChanged();
        StatusMessage = "已从成功密码列表删除当前密码。";
    }

    private bool CanDeleteSavedPassword() => SavedPasswords.Any(password =>
        string.Equals(password, NewTaskPassword, StringComparison.Ordinal));

    [RelayCommand(CanExecute = nameof(CanCancelTask))]
    private void CancelTask(ExtractionTaskViewModel? task)
    {
        if (task is null || !_cancellations.TryGetValue(task.Id, out var cancellation))
        {
            return;
        }

        task.Status = ExtractionStatus.Cancelling;
        StatusMessage = $"正在取消 {task.ArchiveName}…";
        cancellation.Cancel();
        CancelTaskCommand.NotifyCanExecuteChanged();
    }

    private static bool CanCancelTask(ExtractionTaskViewModel? task) => task?.CanCancel == true;

    [RelayCommand]
    private void OpenSource(ExtractionTaskViewModel? task) => ExecuteExplorerAction(
        task,
        selected => _fileExplorerService.ShowFileInFolder(selected.ArchivePath),
        "已在资源管理器中定位源压缩文件。");

    [RelayCommand]
    private void OpenDestination(ExtractionTaskViewModel? task) => ExecuteExplorerAction(
        task,
        selected => _fileExplorerService.OpenFolder(selected.DestinationPath),
        "已打开解压目标目录。");

    [RelayCommand(CanExecute = nameof(CanRemoveTask))]
    private void RemoveTask(ExtractionTaskViewModel? task)
    {
        if (task is null || !task.CanRemove)
        {
            return;
        }

        task.PropertyChanged -= OnTaskPropertyChanged;
        Tasks.Remove(task);
        SaveHistory();
        StatusMessage = $"已移除 {task.ArchiveName} 的任务记录。";
        NotifySummaryChanged();
    }

    private static bool CanRemoveTask(ExtractionTaskViewModel? task) => task?.CanRemove == true;

    [RelayCommand]
    private void ClearFinished()
    {
        var finished = Tasks.Where(task => task.CanRemove).ToArray();
        foreach (var task in finished)
        {
            task.PropertyChanged -= OnTaskPropertyChanged;
            Tasks.Remove(task);
        }

        SaveHistory();
        StatusMessage = finished.Length == 0 ? "没有可清理的已结束任务。" : $"已清理 {finished.Length} 条任务记录。";
        NotifySummaryChanged();
    }

    [RelayCommand]
    private void ShowDetails(ExtractionTaskViewModel? task)
    {
        if (task is null)
        {
            return;
        }

        DetailsTitle = task.ArchiveName;
        DetailsText = string.IsNullOrWhiteSpace(task.DetailText)
            ? "该任务暂时没有详细日志。"
            : task.DetailText;
        IsDetailsOpen = true;
    }

    [RelayCommand]
    private void CloseDetails() => IsDetailsOpen = false;

    public void CancelAll()
    {
        foreach (var cancellation in _cancellations.Values.ToArray())
        {
            cancellation.Cancel();
        }
    }

    private void StartTask(ExtractionTaskViewModel task)
    {
        var cancellation = new CancellationTokenSource();
        _cancellations[task.Id] = cancellation;
        _ = ExecuteTaskAsync(task, SavedPasswords.ToArray(), cancellation);
    }

    private async Task ExecuteTaskAsync(
        ExtractionTaskViewModel task,
        IReadOnlyList<string> savedPasswords,
        CancellationTokenSource cancellation)
    {
        var progress = new Progress<ExtractionProgress>(task.ApplyProgress);
        var lifecycle = new Progress<ExtractionLifecycle>(update =>
        {
            if (update.Stage == ExtractionLifecycleStage.WorkerStarted)
            {
                task.MarkWorkerStarted();
            }

            if (update.Stage == ExtractionLifecycleStage.ProcessStarted)
            {
                task.ProcessId = update.ProcessId;
            }
        });
        var attemptLifecycle = new Progress<PasswordAttemptLifecycle>(update =>
        {
            if (update.AttemptNumber <= 1)
            {
                return;
            }

            task.Progress = 0;
            task.CurrentFile = $"正在尝试成功密码 {update.AttemptNumber - 1}/{update.TotalAttempts - 1}…";
            StatusMessage = $"{task.ArchiveName} 首次解压失败，正在尝试成功密码 " +
                            $"{update.AttemptNumber - 1}/{update.TotalAttempts - 1}。";
        });

        try
        {
            var retryResult = await _passwordRetryService.RunAsync(
                new ExtractionRequest(
                    task.Id,
                    task.ArchivePath,
                    task.DestinationPath,
                    task.Engine,
                    task.Password),
                savedPasswords,
                progress,
                lifecycle,
                attemptLifecycle,
                cancellation.Token);

            var result = retryResult.FinalResult;
            task.DetailText = BuildDetailText(retryResult);
            if (result.IsSuccessful)
            {
                task.SetPassword(retryResult.EffectivePassword);
                RememberSuccessfulPassword(retryResult.EffectivePassword);
            }

            if (result.ExitKind == ExtractionExitKind.Success)
            {
                task.Progress = 100;
                task.Status = ExtractionStatus.Completed;
                StatusMessage = retryResult.Attempts.Count == 1
                    ? $"{task.ArchiveName} 解压完成。"
                    : $"{task.ArchiveName} 使用成功密码解压完成，共尝试 {retryResult.Attempts.Count} 次。";
            }
            else if (result.ExitKind == ExtractionExitKind.Warning)
            {
                task.Progress = 100;
                task.Status = ExtractionStatus.CompletedWithWarning;
                task.ErrorMessage = result.Summary;
                StatusMessage = retryResult.Attempts.Count == 1
                    ? $"{task.ArchiveName} 已完成，但包含警告。"
                    : $"{task.ArchiveName} 使用成功密码完成，但包含警告。";
            }
            else
            {
                task.Status = ExtractionStatus.Failed;
                task.ErrorMessage = BuildShortError(result);
                StatusMessage = retryResult.Attempts.Count == 1
                    ? $"{task.ArchiveName} 解压失败。"
                    : $"{task.ArchiveName} 解压失败，已尝试 {retryResult.Attempts.Count} 个密码。";
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            task.Status = ExtractionStatus.Cancelled;
            task.ErrorMessage = null;
            task.DetailText = BuildLocalDetailText(
                task,
                $"任务由用户取消；对应的 {task.EngineLabel} 进程树已停止。");
            StatusMessage = $"已取消 {task.ArchiveName}。";
        }
        catch (Exception exception)
        {
            task.Status = ExtractionStatus.Failed;
            task.ErrorMessage = exception.Message;
            task.DetailText = BuildLocalDetailText(task, exception.ToString());
            StatusMessage = $"{task.ArchiveName} 启动失败：{exception.Message}";
        }
        finally
        {
            task.MarkEnded();
            task.ProcessId = null;
            _cancellations.Remove(task.Id);
            cancellation.Dispose();
            SaveHistory();
            NotifySummaryChanged();
            CancelTaskCommand.NotifyCanExecuteChanged();
            RemoveTaskCommand.NotifyCanExecuteChanged();
        }
    }

    private static string BuildDetailText(PasswordRetryResult retryResult)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"PASSWORD ATTEMPTS: {retryResult.Attempts.Count}");
        foreach (var attempt in retryResult.Attempts)
        {
            var result = attempt.Result;
            builder.AppendLine();
            builder.AppendLine(new string('=', 72));
            builder.AppendLine($"ATTEMPT {attempt.AttemptNumber}/{retryResult.Attempts.Count}");
            builder.AppendLine($"ENGINE: {GetEngineLabel(result.Engine)}");
            builder.AppendLine($"PASSWORD (PLAINTEXT): {FormatPassword(attempt.Password)}");
            builder.AppendLine();
            builder.AppendLine("COMMAND");
            builder.AppendLine(result.Command);
            builder.AppendLine();
            builder.AppendLine($"EXIT CODE: {result.ExitCode} ({result.ExitKind})");
            builder.AppendLine(result.Summary);
            builder.AppendLine();
            builder.AppendLine("STDOUT");
            builder.AppendLine(result.StandardOutput);
            builder.AppendLine();
            builder.AppendLine("STDERR");
            builder.AppendLine(result.StandardError);
        }

        return builder.ToString();
    }

    private void RememberSuccessfulPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return;
        }

        var existing = SavedPasswords.FirstOrDefault(saved =>
            string.Equals(saved, password, StringComparison.Ordinal));
        if (existing is not null)
        {
            SavedPasswords.Remove(existing);
        }

        SavedPasswords.Insert(0, password);
        while (SavedPasswords.Count > MaxSavedPasswords)
        {
            SavedPasswords.RemoveAt(SavedPasswords.Count - 1);
        }

        DeleteSavedPasswordCommand.NotifyCanExecuteChanged();
        SaveSettingsNow();
    }

    private static string BuildLocalDetailText(ExtractionTaskViewModel task, string message) =>
        $"ENGINE: {task.EngineLabel}{Environment.NewLine}" +
        $"PASSWORD (PLAINTEXT): {FormatPassword(task.Password)}{Environment.NewLine}{Environment.NewLine}" +
        message;

    private static string FormatPassword(string password) =>
        string.IsNullOrEmpty(password) ? "(none)" : password;

    private static string GetEngineLabel(ExtractionEngine engine) =>
        engine == ExtractionEngine.SevenZip ? "7-Zip" : "WinRAR";

    private static string BuildShortError(ExtractionResult result)
    {
        var firstErrorLine = result.StandardError
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstErrorLine)
            ? result.Summary
            : $"{result.Summary} · {firstErrorLine}";
    }

    private void ExecuteExplorerAction(
        ExtractionTaskViewModel? task,
        Action<ExtractionTaskViewModel> action,
        string successMessage)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            action(task);
            StatusMessage = successMessage;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        SelectedEngine = settings.DefaultEngine;
        Theme = settings.Theme;
        ViewMode = settings.ViewMode;
        NewTaskPassword = settings.Password ?? string.Empty;
        SavedPasswords.Clear();
        foreach (var password in (settings.SavedPasswords ?? [])
                     .Where(password => !string.IsNullOrEmpty(password))
                     .Distinct(StringComparer.Ordinal)
                     .Take(MaxSavedPasswords))
        {
            SavedPasswords.Add(password);
        }

        if (!string.IsNullOrWhiteSpace(settings.SevenZipPath))
        {
            _sevenZipLocator.TryUse(settings.SevenZipPath);
        }

        if (!string.IsNullOrWhiteSpace(settings.WinRarPath))
        {
            _winRarLocator.TryUse(settings.WinRarPath);
        }

        _themeService.Apply(Theme);
    }

    private void RestoreHistory()
    {
        foreach (var record in _historyService.Load().Take(MaxHistoryRecords))
        {
            var task = new ExtractionTaskViewModel(record);
            if (task.Status == ExtractionStatus.Interrupted && task.EndTime is null)
            {
                task.MarkEnded();
            }

            task.PropertyChanged += OnTaskPropertyChanged;
            Tasks.Add(task);
        }

        SaveHistory();
        NotifySummaryChanged();
    }

    private void RefreshEngineStatus()
    {
        var executable = SelectedEngine == ExtractionEngine.SevenZip
            ? _sevenZipLocator.Locate()
            : _winRarLocator.Locate();
        IsSelectedEngineAvailable = executable is not null;
        EngineStatusText = executable is null
            ? SelectedEngine == ExtractionEngine.SevenZip
                ? "未找到 7-Zip"
                : "未找到 WinRAR"
            : $"{GetEngineLabel(SelectedEngine)} 已就绪 · {executable}";
    }

    private void QueueSettingsSave()
    {
        if (_isInitializing || _disposed)
        {
            return;
        }

        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void OnSettingsSaveTimerTick(object? sender, EventArgs eventArgs)
    {
        _settingsSaveTimer.Stop();
        SaveSettingsNow();
    }

    private void SaveSettingsNow()
    {
        try
        {
            _settingsService.Save(new AppSettings
            {
                Theme = Theme,
                ViewMode = ViewMode,
                DefaultEngine = SelectedEngine,
                SevenZipPath = _sevenZipLocator.ExecutablePath,
                WinRarPath = _winRarLocator.ExecutablePath,
                Password = NewTaskPassword,
                SavedPasswords = SavedPasswords.ToList()
            });
        }
        catch (IOException exception)
        {
            StatusMessage = $"设置保存失败：{exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            StatusMessage = $"设置保存失败：{exception.Message}";
        }
    }

    private void SaveHistory()
    {
        try
        {
            _historyService.Save(Tasks.Take(MaxHistoryRecords).Select(task => task.ToHistoryRecord()));
        }
        catch (IOException exception)
        {
            StatusMessage = $"历史保存失败：{exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            StatusMessage = $"历史保存失败：{exception.Message}";
        }
    }

    partial void OnSelectedEngineChanged(ExtractionEngine value)
    {
        OnPropertyChanged(nameof(IsSevenZipSelected));
        OnPropertyChanged(nameof(IsWinRarSelected));
        OnPropertyChanged(nameof(ChooseEngineText));
        RefreshEngineStatus();
        QueueSettingsSave();
    }

    partial void OnThemeChanged(AppTheme value)
    {
        _themeService.Apply(value);
        OnPropertyChanged(nameof(ThemeToggleText));
        QueueSettingsSave();
    }

    partial void OnViewModeChanged(TaskViewMode value)
    {
        OnPropertyChanged(nameof(IsListView));
        OnPropertyChanged(nameof(IsTilesView));
        QueueSettingsSave();
    }

    partial void OnNewTaskPasswordChanged(string value)
    {
        DeleteSavedPasswordCommand.NotifyCanExecuteChanged();
        QueueSettingsSave();
    }

    private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ExtractionTaskViewModel.Status))
        {
            NotifySummaryChanged();
            CancelTaskCommand.NotifyCanExecuteChanged();
            RemoveTaskCommand.NotifyCanExecuteChanged();
        }
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(CompletedCount));
    }

    private void OnTimerTick(object? sender, EventArgs eventArgs)
    {
        foreach (var task in Tasks.Where(task => task.Status is ExtractionStatus.Extracting or ExtractionStatus.Cancelling))
        {
            task.RefreshElapsed();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _elapsedTimer.Stop();
        _settingsSaveTimer.Stop();
        SaveSettingsNow();
        SaveHistory();
        CancelAll();
        foreach (var task in Tasks)
        {
            task.PropertyChanged -= OnTaskPropertyChanged;
        }
    }
}
