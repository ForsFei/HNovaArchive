using System.Diagnostics;
using System.Text;
using HDecompressHelper.Infrastructure;
using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public sealed class SevenZipExtractionService(ISevenZipLocator sevenZipLocator) : IExtractionBackend
{
    public ExtractionEngine Engine => ExtractionEngine.SevenZip;

    public Task<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress,
        IProgress<ExtractionLifecycle>? lifecycle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (archivePath, destinationPath) = ValidateRequest(request);
        var executablePath = sevenZipLocator.ExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new FileNotFoundException("未找到 7z.exe，请先选择 7-Zip 命令行程序。", "7z.exe");
        }

        Directory.CreateDirectory(destinationPath);
        var startInfo = CreateStartInfo(executablePath, archivePath, destinationPath, request.Password);
        var command = BuildDisplayCommand(executablePath, archivePath, destinationPath, request.Password);

        return CliProcessRunner.RunAsync(
            Engine,
            startInfo,
            command,
            new SevenZipProgressParser(),
            SevenZipExitCodeMapper.Map,
            progress,
            lifecycle,
            cancellationToken);
    }

    internal static ProcessStartInfo CreateStartInfo(
        string executablePath,
        string archivePath,
        string destinationPath,
        string? password)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("x");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add($"-o{destinationPath}");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-bsp1");
        startInfo.ArgumentList.Add("-bso1");
        startInfo.ArgumentList.Add("-bse2");
        startInfo.ArgumentList.Add("-bb1");
        startInfo.ArgumentList.Add("-sccUTF-8");
        if (!string.IsNullOrEmpty(password))
        {
            startInfo.ArgumentList.Add($"-p{password}");
        }

        return startInfo;
    }

    internal static string BuildDisplayCommand(
        string executablePath,
        string archivePath,
        string destinationPath,
        string? password)
    {
        var passwordArgument = string.IsNullOrEmpty(password)
            ? string.Empty
            : $" -p{CommandLineDisplay.Quote(password)}";
        return $"{CommandLineDisplay.Quote(executablePath)} x {CommandLineDisplay.Quote(archivePath)} " +
               $"-o{CommandLineDisplay.Quote(destinationPath)} -y -bsp1 -bso1 -bse2 -bb1 -sccUTF-8{passwordArgument}";
    }

    private static (string ArchivePath, string DestinationPath) ValidateRequest(ExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var archivePath = Path.GetFullPath(request.ArchivePath);
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("找不到要解压的压缩文件。", archivePath);
        }

        return (archivePath, Path.GetFullPath(request.DestinationPath));
    }
}

