using Microsoft.Win32;
using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public sealed class FileDialogService : IFileDialogService
{
    public IReadOnlyList<string> PickArchives()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要解压的文件",
            Multiselect = true,
            CheckFileExists = true,
            Filter = "压缩文件|*.zip;*.7z;*.rar;*.7z.001;*.zip.001;*.rar.001;*.tar;*.gz;*.gzip;*.tgz;*.xz;*.txz;*.bz2;*.tbz2;*.cab;*.iso;*.wim|所有文件（自动改为 .zip）|*.*"
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? PickExtractionExecutable(ExtractionEngine engine)
    {
        var isSevenZip = engine == ExtractionEngine.SevenZip;
        var dialog = new OpenFileDialog
        {
            Title = isSevenZip ? "选择 7z.exe" : "选择 WinRAR 命令行程序",
            Multiselect = false,
            CheckFileExists = true,
            FileName = isSevenZip ? "7z.exe" : "Rar.exe",
            Filter = isSevenZip
                ? "7-Zip 命令行程序 (7z.exe)|7z.exe|可执行文件|*.exe"
                : "WinRAR 命令行程序|Rar.exe;UnRAR.exe;WinRAR.exe|可执行文件|*.exe"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
