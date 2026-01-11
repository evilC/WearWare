using WearWare.Services.Library;

namespace WearWare.Common.Media
{
    public class PlayableItem : LibraryItem
    {
        public PlayMode PlayMode { get; set; } = PlayMode.LOOP;
        public int PlayModeValue { get; set; } = 1;
        public string ParentFolder { get; init; }
        public bool Enabled { get; set; } = true;

        public PlayableItem(string name, string parentFolder, MediaType mediaType, string sourceFileName, PlayMode playMode, int playModeValue)
            : base(name, mediaType, sourceFileName)
        {
            ParentFolder = parentFolder;
            PlayMode = playMode;
            PlayModeValue = playModeValue;
        }
    }
}
