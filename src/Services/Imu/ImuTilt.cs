using System.Numerics;

namespace WearWare.Services.Imu
{
    internal static class ImuTilt
    {
        public static double CalculateDegrees(Vector3 acceleration)
        {
            return Math.Atan2(
                -acceleration.X,
                Math.Sqrt(acceleration.Y * acceleration.Y + acceleration.Z * acceleration.Z)) * 180 / Math.PI;
        }
    }
}
