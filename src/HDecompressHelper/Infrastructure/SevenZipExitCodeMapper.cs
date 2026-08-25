using HDecompressHelper.Models;

namespace HDecompressHelper.Infrastructure;

public static class SevenZipExitCodeMapper
{
    public static (ExtractionExitKind Kind, string Summary) Map(int exitCode) => exitCode switch
    {
        0 => (ExtractionExitKind.Success, "解压完成"),
        1 => (ExtractionExitKind.Warning, "解压完成，但 7-Zip 报告了非致命警告"),
        2 => (ExtractionExitKind.FatalError, "7-Zip 报告致命错误，压缩包可能损坏或密码不正确"),
        7 => (ExtractionExitKind.CommandLineError, "7-Zip 命令行参数错误"),
        8 => (ExtractionExitKind.OutOfMemory, "可用内存不足，7-Zip 无法继续"),
        255 => (ExtractionExitKind.Stopped, "7-Zip 已停止当前操作"),
        _ => (ExtractionExitKind.UnknownError, $"7-Zip 异常退出（代码 {exitCode}）")
    };
}
