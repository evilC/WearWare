using WearWare.Config;

namespace WearWare.Common.Media
{
    public class PlayableItem
    {
        public string Name { get; init; }
        public MediaType MediaType { get; init; }
        public string SourceFileName { get; init; }
        public int RelativeBrightness { get; set; } = 100;
        public int CurrentBrightness { get; set; } = 100;
        public PlayMode PlayMode { get; set; } = PlayMode.LOOP;
        public int PlayModeValue { get; set; } = 1;
        public string ParentFolder { get; init; }
        public bool Enabled { get; set; } = true;

        public PlayableItem(string name, 
            string parentFolder, 
            MediaType mediaType, 
            string sourceFileName, 
            PlayMode playMode, 
            int playModeValue, 
            int relativeBrightness, 
            int currentBrightness)
        {
            Name = name;
            MediaType = mediaType;
            SourceFileName = sourceFileName;
            ParentFolder = parentFolder;
            PlayMode = playMode;
            PlayModeValue = playModeValue;
            RelativeBrightness = relativeBrightness;
            CurrentBrightness = currentBrightness;
        }

        public string GetStreamFilePath()
        {
            return Path.Combine(PathConfig.Root, ParentFolder, $"{Name}.stream");
        }

        public string GetSourceFilePath()
        {
            return Path.Combine(PathConfig.Root, ParentFolder, SourceFileName);
        }
    }
}
