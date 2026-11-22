using System.Device.I2c;
using System.Numerics;
using Iot.Device.Imu;

namespace WearWare.Services.Physics.Imu.Sensors
{
    public sealed class Mpu6050Sensor : IImuSensor
    {
        private readonly Mpu6050 _sensor;

        public Mpu6050Sensor()
        {
            var device = I2cDevice.Create(new I2cConnectionSettings(1, Mpu6050.DefaultI2cAddress));
            try
            {
                _sensor = new Mpu6050(device);
            }
            catch
            {
                device.Dispose();
                throw;
            }
        }

        public (Vector3 Acceleration, Vector3 Gyroscope) Read()
        {
            return (_sensor.GetAccelerometer(), _sensor.GetGyroscopeReading());
        }

        public void Dispose() => _sensor.Dispose();
    }
}
