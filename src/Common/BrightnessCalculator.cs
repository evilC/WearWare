namespace WearWare.Common
{
    public static class BrightnessCalculator
    {
        public static int CalculateAbsoluteBrightness(int baseBrightness, int relativeBrightness)
        {
            // Ensure relative brightness is between 0 and 100
            relativeBrightness = Math.Clamp(relativeBrightness, 0, 100);
            // Calculate absolute brightness
            return baseBrightness * relativeBrightness / 100;
        }
    }
}