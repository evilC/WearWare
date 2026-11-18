using System.Diagnostics;
using System.Numerics;

namespace WearWare.Services.Imu
{
    /// <summary>Acceleration is in m/s^2 and gyroscope values are in degrees per second, in sensor axes.</summary>
    public sealed record ImuReading(
        Vector3 Acceleration,
        Vector3 Gyroscope,
        double RawTiltDegrees,
        double FilteredTiltDegrees,
        DateTimeOffset Timestamp,
        long TimestampTicks)
    {
        /// <summary>Elapsed time since the sample was read, measured with a monotonic clock.</summary>
        public TimeSpan Age => Stopwatch.GetElapsedTime(TimestampTicks);
    }

    public enum ImuStatus
    {
        Unavailable,
        Starting,
        Available,
        Stale,
        Failed,
        Stopped
    }

    public sealed record ImuState(ImuStatus Status, ImuReading? Reading);
}
