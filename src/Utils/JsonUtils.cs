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
    }
}