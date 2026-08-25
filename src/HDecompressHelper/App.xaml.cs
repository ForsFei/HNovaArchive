using System.Windows;
using HDecompressHelper.Services;
using HDecompressHelper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HDecompressHelper;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<ISevenZipLocator, SevenZipLocator>();
        services.AddSingleton<IWinRarLocator, WinRarLocator>();
        services.AddSingleton<IExtractionBackend, SevenZipExtractionService>();
        services.AddSingleton<IExtractionBackend, WinRarExtractionService>();
        services.AddSingleton<IExtractionService, ExtractionServiceRouter>();
        services.AddSingleton<IExtractionQueueService>(provider =>
            new ExtractionQueueService(provider.GetRequiredService<IExtractionService>(), maxConcurrentExtractions: 2));
        services.AddSingleton<IPasswordRetryService, PasswordRetryService>();
        services.AddSingleton<IFileExplorerService, FileExplorerService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IHistoryService, JsonHistoryService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
        _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        _serviceProvider.GetRequiredService<MainWindow>().Show();

        if (e.Args.Length > 0)
        {
            _mainViewModel.AddArchives(e.Args);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Cancellation callbacks synchronously terminate the owned 7z process trees.
        // Provider disposal is left to process teardown so workers cannot race a
        // disposed SemaphoreSlim while they complete the cancellation path.
        _mainViewModel?.Dispose();
        base.OnExit(e);
    }
}
