using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;
using HDecompressHelper.Services;
using Microsoft.Win32;

namespace HDecompressHelper.ViewModels;

public sealed partial class MainViewModel
{
    private readonly ArchiveWorkflowScanner _workflowScanner = new();
    private readonly Dictionary<string, string> _workflowPending = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _passwordDialogGate = new(1, 1);
    private readonly CancellationTokenSource _workflowStop = new();
    private DispatcherTimer? _workflowTimer;
    private bool _scanning;

    public ObservableCollection<string> WatchedFolders { get; } = [];

    [ObservableProperty] private bool _isWorkflowOpen;
    [ObservableProperty] private bool _folderMonitoringEnabled;
    [ObservableProperty] private string _workflowStatus = "监控已暂停";

    private void InitializeWorkflow()
    {
        _workflowTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Normal,
            async (_, _) => await ScanWorkflowAsync(), Dispatcher.CurrentDispatcher);
        _workflowTimer.Start();
        WorkflowStatus = FolderMonitoringEnabled ? "正在监控，等待文件稳定…" : "监控已暂停";
    }

    [RelayCommand] private void OpenWorkflow() => IsWorkflowOpen = true;
    [RelayCommand] private void CloseWorkflow() => IsWorkflowOpen = false;
    [RelayCommand] private async Task ScanWorkflowNowAsync() => await ScanWorkflowAsync();

    [RelayCommand]
    private void AddWatchedFolder()
    {
        var dialog = new OpenFolderDialog { Title = "选择自动解压监控目录", Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        foreach (var folder in dialog.FolderNames)
            if (!WatchedFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)) WatchedFolders.Add(folder);
        SaveSettingsNow();
        RequestWorkflowScan();
    }

    private void RequestWorkflowScan()
    {
        if (_workflowTimer is not null && FolderMonitoringEnabled) _ = ScanWorkflowAsync();
    }

    [RelayCommand]
    private void RemoveWatchedFolder(string? folder)
    {
        if (folder is null) return;
        WatchedFolders.Remove(folder);
        SaveSettingsNow();
    }

    partial void OnFolderMonitoringEnabledChanged(bool value)
    {
        WorkflowStatus = value ? "正在监控，等待文件稳定…" : "监控已暂停（已排队任务继续执行）";
        QueueSettingsSave();
        RequestWorkflowScan();
    }

    private async Task ScanWorkflowAsync()
    {
        if (_scanning || _disposed || !FolderMonitoringEnabled || WatchedFolders.Count == 0) return;
        _scanning = true;
        try
        {
            RefreshEngineStatus();
            if (!IsSelectedEngineAvailable) { WorkflowStatus = "请先选择可用的解压引擎"; return; }
            var roots = WatchedFolders.ToArray();
            var blocked = Tasks.Where(t => !t.CanRemove).Select(t => t.DestinationPath).ToArray();
            var files = await Task.Run(() => _workflowScanner.Scan(roots, blocked, DateTimeOffset.UtcNow, _workflowStop.Token));
            if (_disposed || !FolderMonitoringEnabled) return;
            var added = 0;
            var enqueueErrors = new List<string>();
            foreach (var file in files)
            {
                if (ActiveCount >= 10) break;
                if (_workflowPending.ContainsKey(file) || Tasks.Any(t => !t.CanRemove &&
                    string.Equals(t.ArchivePath, file, StringComparison.OrdinalIgnoreCase))) continue;
                try
                {
                    // Recheck after the background scan, before any rename.
                    using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        if (!_workflowScanner.IsUnchangedSinceScan(file)) continue;
                    }
                    var prepared = ArchiveInputNormalizer.Prepare(file);
                    var stamp = ArchiveWorkflowScanner.Stamp(prepared.ArchivePath);
                    _workflowPending[prepared.ArchivePath] = stamp;
                    AddArchives([prepared.ArchivePath]);
                    if (!Tasks.Any(t => !t.CanRemove && t.ArchivePath.Equals(prepared.ArchivePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        _workflowPending.Remove(prepared.ArchivePath);
                        continue;
                    }
                    added++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { enqueueErrors.Add($"{Path.GetFileName(file)}：{ex.Message}"); }
            }
            WorkflowStatus = $"{DateTime.Now:HH:mm:ss} · {_workflowScanner.LastScanSummary} · 本轮排队 {added}";
            if (enqueueErrors.Count > 0) WorkflowStatus += $" · 排队失败 {enqueueErrors.Count}：{enqueueErrors[0]}";
            try
            {
                JsonFileStorage.Save(Path.Combine(JsonFileStorage.GetDefaultDataDirectory(), "workflow-status.json"),
                    new { LastScan = DateTimeOffset.Now, Status = WorkflowStatus });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { WorkflowStatus += $" · 诊断保存失败：{ex.Message}"; }
        }
        catch (OperationCanceledException) when (_workflowStop.IsCancellationRequested) { }
        catch (Exception ex) { WorkflowStatus = $"扫描失败，将自动重试：{ex.Message}"; }
        finally { _scanning = false; }
    }

    private void FinishWorkflowTask(ExtractionTaskViewModel task)
    {
        if (!_workflowPending.Remove(task.ArchivePath, out var stamp)) return;
        try { _workflowScanner.MarkProcessed(task.ArchivePath, stamp); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { WorkflowStatus = $"工作流记录保存失败：{ex.Message}"; }
    }

    internal static bool NeedsPasswordPrompt(ExtractionResult result)
    {
        if (result.IsSuccessful) return false;
        var text = result.StandardOutput + "\n" + result.StandardError;
        return text.Contains("Wrong password", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("password is incorrect", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Enter password", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("需要密码", StringComparison.Ordinal) || text.Contains("密码不正确", StringComparison.Ordinal) ||
               result.Summary == PasswordFailureClassifier.MissingPasswordSummary ||
               (result.Engine == ExtractionEngine.WinRar && result.ExitCode == 11);
    }

    private async Task<string?> PromptPasswordAsync(ExtractionTaskViewModel task, CancellationToken token)
    {
        await _passwordDialogGate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var password = new TextBox { Text = task.Password, Margin = new Thickness(0, 14, 0, 14), MinWidth = 380, Padding = new Thickness(8) };
            password.SetResourceReference(Control.BackgroundProperty, "Surface1Brush");
            password.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
            password.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            var panel = new StackPanel { Margin = new Thickness(22) };
            panel.Children.Add(new TextBlock { Text = $"{task.ArchiveName}\n密码列表已尝试完毕，请输入密码重试。", TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(password);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var retry = new Button { Content = "重试", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "跳过", IsCancel = true, MinWidth = 80 };
            retry.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButtonStyle");
            cancel.SetResourceReference(FrameworkElement.StyleProperty, "SecondaryButtonStyle");
            buttons.Children.Add(retry); buttons.Children.Add(cancel); panel.Children.Add(buttons);
            var dialog = new Window { Title = "解压密码", Content = panel, SizeToContent = SizeToContent.WidthAndHeight,
                Owner = Application.Current?.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
            dialog.SetResourceReference(Control.BackgroundProperty, "Surface0Brush");
            dialog.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
            retry.Click += (_, _) => { if (password.Text.Length > 0) dialog.DialogResult = true; };
            dialog.Loaded += (_, _) => { password.Focus(); password.SelectAll(); };
            using var registration = token.Register(() => dialog.Dispatcher.BeginInvoke(new Action(dialog.Close)));
            return dialog.ShowDialog() == true ? password.Text : null;
        }
        finally { _passwordDialogGate.Release(); }
    }
}
