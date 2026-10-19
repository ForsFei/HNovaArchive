using System.Diagnostics;
using System.Globalization;
using System.Text;
using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public sealed class WinRarExtractionService(IWinRarLocator winRarLocator) : IExtractionBackend
{
    public ExtractionEngine Engine => ExtractionEngine.WinRar;

    public async Task<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var archivePath = Path.GetFullPath(request.ArchivePath);
        var destinationPath = Path.GetFullPath(request.DestinationPath);
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("找不到要解压的压缩文件。", archivePath);
        }

        var executablePath = winRarLocator.ExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new FileNotFoundException("未找到 Rar.exe、UnRAR.exe 或 WinRAR.exe，请先选择 WinRAR 命令行程序。", "Rar.exe");
        }

        Directory.CreateDirectory(destinationPath);
        var startInfo = CreateStartInfo(executablePath, archivePath, destinationPath, request.Password);
        var command = BuildDisplayCommand(executablePath, archivePath, destinationPath, request.Password);

        var result = await CliProcessRunner.RunAsync(
            Engine,
            startInfo,
            command,
            new WinRarProgressParser(),
            WinRarExitCodeMapper.Map,
            progress,
            lifecycle,
            cancellationToken);

        return PasswordFailureClassifier.NormalizeMissingPassword(result, request.Password);
    }

    internal static ProcessStartInfo CreateStartInfo(
        string executablePath,
        string archivePath,
        string destinationPath,
        string? password)
    {
        var consoleEncoding = GetConsoleEncoding();
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = consoleEncoding,
            StandardErrorEncoding = consoleEncoding
        };

        startInfo.ArgumentList.Add("x");
        startInfo.ArgumentList.Add("-o+");
        startInfo.ArgumentList.Add("-y");
        if (string.Equals(Path.GetFileName(executablePath), "WinRAR.exe", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("-ibck");
        }

        startInfo.ArgumentList.Add(string.IsNullOrEmpty(password) ? "-p-" : $"-p{password}");

        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add(Path.EndsInDirectorySeparator(destinationPath)
            ? destinationPath
            : destinationPath + Path.DirectorySeparatorChar);
        return startInfo;
    }

    internal static string BuildDisplayCommand(
        string executablePath,
        string archivePath,
        string destinationPath,
        string? password)
    {
        var backgroundArgument = string.Equals(
            Path.GetFileName(executablePath),
            "WinRAR.exe",
            StringComparison.OrdinalIgnoreCase)
            ? " -ibck"
            : string.Empty;
        var passwordArgument = string.IsNullOrEmpty(password)
            ? " -p-"
            : $" -p{CommandLineDisplay.Quote(password)}";
        var destinationArgument = Path.EndsInDirectorySeparator(destinationPath)
            ? destinationPath
            : destinationPath + Path.DirectorySeparatorChar;
        return $"{CommandLineDisplay.Quote(executablePath)} x -o+ -y{backgroundArgument}{passwordArgument} " +
               $"{CommandLineDisplay.Quote(archivePath)} {CommandLineDisplay.Quote(destinationArgument)}";
    }

    private static Encoding GetConsoleEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
