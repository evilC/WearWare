using System.Numerics;

namespace WearWare.Architecture.Physics.Imu.Sensors
{
    public interface IImuSensor : IDisposable
    {
        /// <summary>Reads acceleration in m/s^2 and angular velocity in degrees per second, in sensor axes.</summary>
        (Vector3 Acceleration, Vector3 Gyroscope) Read();
    }
}
