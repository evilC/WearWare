using System.Diagnostics;
using System.Numerics;

namespace WearWare.Services.Imu
{
    /// <summary>Acceleration is in m/s^2 and gyroscope values are in degrees per second.</summary>
    public sealed record ImuReading(
        // Raw sensor axes, followed by axes adjusted for the sensor's mounting orientation.
        Vector3 Acceleration,           // Raw accelerometer reading
        Vector3 Gyroscope,              // Raw gyroscope reading
        Vector3 MountedAcceleration,    // Accelerometer reading adjusted for sensor mounting orientation
        Vector3 MountedGyroscope,       // Gyroscope reading adjusted for sensor mounting orientation
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
