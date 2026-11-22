using System.Text.Json.Serialization;

namespace WearWare.Utils
{
    public static class JsonUtils
    {
        static JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            IndentCharacter = '\t',
            IndentSize = 1,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static T? FromJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _serializerOptions);
        }

        public static string ToJson<T>(T obj)
        {
            return JsonSerializer.Serialize<T>(obj, _serializerOptions);
        }

        public static T? FromJsonFile<T>(string path)
        {
            var json = File.ReadAllText(path);
            return FromJson<T>(json);
        }

        public static void ToJsonFile<T>(string path, T obj)
        {
            File.WriteAllText(path, ToJson(obj));
        }

        public static void ToJsonFileAtomic<T>(string path, T obj, ILogger logger)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                ToJsonFile(temporaryPath, obj);
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not remove temporary state file {path}.", temporaryPath);
                }
            }
        }
    }
}