namespace HDecompressHelper.Services;

public interface IFileExplorerService
{
    void OpenFolder(string folderPath);

    void ShowFileInFolder(string filePath);
}

