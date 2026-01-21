using WearWare.Config;
using WearWare.Services.Library;

namespace WearWare.Common.Media
{
    public class PlayableItem : LibraryItem
    {
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
            : base(name, mediaType, sourceFileName, relativeBrightness, currentBrightness)
        {
            ParentFolder = parentFolder;
            PlayMode = playMode;
            PlayModeValue = playModeValue;
            RelativeBrightness = relativeBrightness;
        }

        public new string GetStreamFilePath()
        {
            return Path.Combine(PathConfig.Root, ParentFolder, $"{Name}.stream");
        }

        public new string GetSourceFilePath()
        {
            return Path.Combine(PathConfig.Root, ParentFolder, SourceFileName);
        }
    }
}
