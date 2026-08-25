using HDecompressHelper.Models;

namespace HDecompressHelper.Infrastructure;

public static class WinRarExitCodeMapper
{
    public static (ExtractionExitKind Kind, string Summary) Map(int exitCode) => exitCode switch
    {
        0 => (ExtractionExitKind.Success, "解压完成"),
        1 => (ExtractionExitKind.Warning, "WinRAR 完成操作，但报告了非致命警告"),
        2 => (ExtractionExitKind.FatalError, "WinRAR 报告致命错误"),
        3 => (ExtractionExitKind.FatalError, "压缩包 CRC 校验失败，文件可能损坏或密码错误"),
        4 => (ExtractionExitKind.OpenError, "压缩包被锁定，WinRAR 无法访问"),
        5 => (ExtractionExitKind.WriteError, "WinRAR 无法写入目标文件"),
        6 => (ExtractionExitKind.OpenError, "WinRAR 无法打开输入文件"),
        7 => (ExtractionExitKind.UserError, "WinRAR 命令参数或用户选项错误"),
        8 => (ExtractionExitKind.OutOfMemory, "可用内存不足，WinRAR 无法继续"),
        9 => (ExtractionExitKind.WriteError, "WinRAR 无法创建输出文件"),
        10 => (ExtractionExitKind.Warning, "没有找到符合条件的文件"),
        11 => (ExtractionExitKind.FatalError, "压缩包密码不正确"),
        12 => (ExtractionExitKind.ReadError, "WinRAR 读取文件时发生错误"),
        255 => (ExtractionExitKind.Stopped, "WinRAR 已由用户停止"),
        _ => (ExtractionExitKind.UnknownError, $"WinRAR 异常退出（代码 {exitCode}）")
    };
}

