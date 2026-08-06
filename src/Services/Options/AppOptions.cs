namespace WearWare.Services.Options
{
    public class AppOptions
    {
        public int? CurrentBrightness { get; set; }

        public AppOptions Clone()
        {
            return new AppOptions
            {
                CurrentBrightness = CurrentBrightness
            };
        }
    }
}
