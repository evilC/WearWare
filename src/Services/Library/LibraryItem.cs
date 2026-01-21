using WearWare.Common.Media;
using WearWare.Config;

namespace WearWare.Services.Library
{
    public class LibraryItem
    {
        public string Name { get; init; }
        public MediaType MediaType { get; init; }
        public string SourceFileName { get; init; }
        public int RelativeBrightness { get; set; } = 100;
        public int CurrentBrightness { get; set; } = 100;

        public LibraryItem(string name, MediaType mediaType, string sourceFileName, int relativeBrightness, int currentBrightness)
        {
            Name = name;
            MediaType = mediaType;
            SourceFileName = sourceFileName;
            RelativeBrightness = relativeBrightness;
            CurrentBrightness = currentBrightness;
        }

        public string GetSourceFilePath()
        {
            return Path.Combine(PathConfig.LibraryPath, SourceFileName);
        }

        public string GetStreamFilePath()
        {
            return Path.Combine(PathConfig.LibraryPath, $"{Name}.stream");
        }
    }
}
