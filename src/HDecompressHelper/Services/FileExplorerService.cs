using System.Diagnostics;

namespace HDecompressHelper.Services;

public sealed class FileExplorerService : IFileExplorerService
{
    public void OpenFolder(string folderPath)
    {
        var fullPath = Path.GetFullPath(folderPath);
        Directory.CreateDirectory(fullPath);

        var startInfo = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(fullPath);
        Process.Start(startInfo);
    }

    public void ShowFileInFolder(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("找不到源压缩文件。", fullPath);
        }

        var startInfo = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add($"/select,{fullPath}");
        Process.Start(startInfo);
    }
}

