using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HDecompressHelper.Infrastructure;

internal static class JsonFileStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Load<T>(string filePath, Func<T> fallback)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return fallback();
            }

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? fallback();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return fallback();
        }
    }

    public static void Save<T>(string filePath, T value)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException("持久化文件没有有效目录。", nameof(filePath));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var json = JsonSerializer.Serialize(value, SerializerOptions);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static string GetDefaultDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HDecompressHelper");
}

