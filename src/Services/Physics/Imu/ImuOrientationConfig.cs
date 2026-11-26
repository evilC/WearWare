using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using WearWare.Utils;

namespace WearWare.Services.Physics.Imu
{
    public enum ImuOrientation
    {
        Default,
        RotateX,
        RotateY,
        RotateZ
    }

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

        [JsonPropertyName("calibrationId")]
        public Guid? CalibrationId { get; set; }

        [JsonIgnore]
        public ImuOrientation Orientation => (X, Y, Z) switch
        {
            (1, 1, 1) => ImuOrientation.Default,
            (1, -1, -1) => ImuOrientation.RotateX,
            (-1, 1, -1) => ImuOrientation.RotateY,
            (-1, -1, 1) => ImuOrientation.RotateZ,
            _ => throw new InvalidOperationException("Invalid IMU orientation.")
        };

        // A new ID prevents an old calibration from returning if the orientation is later changed back.
        public static ImuOrientationConfig Create(ImuOrientation orientation) => orientation switch
        {
            ImuOrientation.Default => new() { X = 1, Y = 1, Z = 1, CalibrationId = Guid.NewGuid() },
            ImuOrientation.RotateX => new() { X = 1, Y = -1, Z = -1, CalibrationId = Guid.NewGuid() },
            ImuOrientation.RotateY => new() { X = -1, Y = 1, Z = -1, CalibrationId = Guid.NewGuid() },
            ImuOrientation.RotateZ => new() { X = -1, Y = -1, Z = 1, CalibrationId = Guid.NewGuid() },
            _ => throw new ArgumentOutOfRangeException(nameof(orientation))
        };

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
