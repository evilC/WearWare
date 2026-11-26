using System.Numerics;

namespace WearWare.Services.Physics.Imu
{
    internal enum OrientationDetectionStatus
    {
        Detected,
        Unsupported,
        NeedLeftTilt,
        Retry
    }

    internal static class ImuOrientationDetection
    {
        private const float AxisConfidence = 0.94f;
        private const float MinimumTilt = 0.22f;

        public static (OrientationDetectionStatus Status, ImuOrientation Orientation) Detect(
            Vector3 upright, Vector3 right, Vector3? left = null)
        {
            upright = Vector3.Normalize(upright);
            right = Vector3.Normalize(right);
            var (yAxis, ySign, yConfidence) = ClosestAxis(upright);
            if (yConfidence < AxisConfidence)
                return (OrientationDetectionStatus.Retry, default);

            var rightHorizontal = right - Vector3.Dot(right, upright) * upright;
            Vector3 xDirection;
            if (left is { } leftReading)
            {
                var normalizedLeft = Vector3.Normalize(leftReading);
                var leftHorizontal = normalizedLeft - Vector3.Dot(normalizedLeft, upright) * upright;
                xDirection = leftHorizontal - rightHorizontal;
                if (leftHorizontal.Length() < MinimumTilt || xDirection.Length() < MinimumTilt ||
                    Vector3.Dot(rightHorizontal, xDirection) > 0.05f ||
                    Vector3.Dot(leftHorizontal, xDirection) <= 0)
                    return (OrientationDetectionStatus.Retry, default);
            }
            else
            {
                if (rightHorizontal.Length() < MinimumTilt)
                    return (OrientationDetectionStatus.NeedLeftTilt, default);
                xDirection = -rightHorizontal;
            }

            var (xAxis, xSign, xConfidence) = ClosestAxis(Vector3.Normalize(xDirection));
            if (xConfidence < AxisConfidence || xAxis == yAxis)
                return (left == null ? OrientationDetectionStatus.NeedLeftTilt : OrientationDetectionStatus.Retry, default);

            if (xAxis != 0 || yAxis != 1)
                return (OrientationDetectionStatus.Unsupported, default);

            var orientation = (xSign, ySign) switch
            {
                (1, 1) => ImuOrientation.Default,
                (1, -1) => ImuOrientation.RotateX,
                (-1, 1) => ImuOrientation.RotateY,
                (-1, -1) => ImuOrientation.RotateZ,
                _ => throw new InvalidOperationException("Invalid detected IMU orientation.")
            };
            return (OrientationDetectionStatus.Detected, orientation);
        }

        private static (int Axis, int Sign, float Confidence) ClosestAxis(Vector3 vector)
        {
            var axis = 0;
            var value = vector.X;
            if (Math.Abs(vector.Y) > Math.Abs(value))
            {
                axis = 1;
                value = vector.Y;
            }
            if (Math.Abs(vector.Z) > Math.Abs(value))
            {
                axis = 2;
                value = vector.Z;
            }
            return (axis, value < 0 ? -1 : 1, Math.Abs(value));
        }
    }
}
