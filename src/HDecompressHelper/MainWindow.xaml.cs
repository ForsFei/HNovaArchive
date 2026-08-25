using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using HDecompressHelper.ViewModels;

namespace HDecompressHelper;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isSynchronizingPassword;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SynchronizePasswordBox();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            _viewModel.AddArchives(files);
        }
    }

    private void OnArchivePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingPassword)
        {
            return;
        }

        _viewModel.NewTaskPassword = ArchivePasswordBox.Password;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.NewTaskPassword))
        {
            SynchronizePasswordBox();
        }
    }

    private void SynchronizePasswordBox()
    {
        if (string.Equals(ArchivePasswordBox.Password, _viewModel.NewTaskPassword, StringComparison.Ordinal))
        {
            return;
        }

        _isSynchronizingPassword = true;
        ArchivePasswordBox.Password = _viewModel.NewTaskPassword;
        _isSynchronizingPassword = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.CancelAll();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }
}
