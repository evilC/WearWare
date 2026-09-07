namespace WearWare.Common
{
    public static class BrightnessCalculator
    {
        public static int CalculateAbsoluteBrightness(int baseBrightness, int relativeBrightness)
        {
            // Ensure inputs are in valid ranges.
            baseBrightness = Math.Clamp(baseBrightness, 1, 100);
            relativeBrightness = Math.Clamp(relativeBrightness, 0, 100);

            // Enforce runtime brightness contract of 1..100.
            var absoluteBrightness = baseBrightness * relativeBrightness / 100;
            return Math.Clamp(absoluteBrightness, 1, 100);
        }
    }
}