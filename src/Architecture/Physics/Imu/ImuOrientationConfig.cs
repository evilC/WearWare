using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using WearWare.Utils;

namespace WearWare.Architecture.Physics.Imu
{
    internal sealed class ImuOrientationConfig
    {
        [JsonPropertyName("x")]
        [JsonRequired]
        public int X { get; set; } = 1;

        [JsonPropertyName("y")]
        [JsonRequired]
        public int Y { get; set; } = 1;

        [JsonPropertyName("z")]
        [JsonRequired]
        public int Z { get; set; } = 1;

        public static ImuOrientationConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                var defaults = new ImuOrientationConfig();
                JsonUtils.ToJsonFile(path, defaults);
                return defaults;
            }

            var config = JsonUtils.FromJsonFile<ImuOrientationConfig>(path)
                ?? throw new JsonException("IMU orientation configuration contains null.");
            if ((config.X is not (1 or -1)) || (config.Y is not (1 or -1)) ||
                (config.Z is not (1 or -1)) || config.X * config.Y * config.Z != 1)
            {
                throw new JsonException("IMU orientation axes must be 1 or -1, with zero or two axes inverted.");
            }
            return config;
        }

        public Vector3 Apply(Vector3 value) => new(value.X * X, value.Y * Y, value.Z * Z);
    }
}
