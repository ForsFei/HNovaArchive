namespace HDecompressHelper.Infrastructure;

internal static class CommandLineDisplay
{
    public static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}

