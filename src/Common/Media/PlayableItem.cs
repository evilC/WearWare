using WearWare.Config;
using WearWare.Services.MatrixConfig;

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
        public LedMatrixOptionsConfig MatrixOptions { get; set; }

        public PlayableItem(string name, 
            string parentFolder, 
            MediaType mediaType, 
            string sourceFileName, 
            PlayMode playMode, 
            int playModeValue, 
            int relativeBrightness, 
            int currentBrightness,
            LedMatrixOptionsConfig matrixOptions)
        {
            Name = name;
            MediaType = mediaType;
            SourceFileName = sourceFileName;
            ParentFolder = parentFolder;
            PlayMode = playMode;
            PlayModeValue = playModeValue;
            RelativeBrightness = relativeBrightness;
            CurrentBrightness = currentBrightness;
            MatrixOptions = matrixOptions;
        }

        public string GetStreamFilePath()
        {
            return Path.Combine(PathConfig.Root, ParentFolder, $"{Name}.stream");
        }

        public string GetSourceFilePath()
        {
            return Path.Combine(PathConfig.Root, ParentFolder, SourceFileName);
        }

        public PlayableItem Clone()
        {
            return new PlayableItem(
                Name,
                ParentFolder,
                MediaType,
                SourceFileName,
                PlayMode,
                PlayModeValue,
                RelativeBrightness,
                CurrentBrightness,
                MatrixOptions.Clone()
            );
        }

        /// <summary>
        /// Determines if we need to reconvert the item based on changes to relevant properties.
        /// </summary>
        /// <param name="other">The other PlayableItem to compare against.</param>
        /// <returns>True if reconversion is needed; otherwise, false.</returns>
        public bool NeedsReConvert(PlayableItem other)
        {
            if (other == null) return false;
            return 
                   RelativeBrightness != other.RelativeBrightness ||
                   !MatrixOptions.IsEqual(other.MatrixOptions);
        }

        /// <summary>
        /// Copies mutable properties from a cloned PlayableItem into this one.
        /// </summary>
        /// <param name="other"></param> A cloned PlayableItem to copy from.
        public void UpdateFromClone(PlayableItem other)
        {
            PlayMode = other.PlayMode;
            PlayModeValue = other.PlayModeValue;
            RelativeBrightness = other.RelativeBrightness;
            CurrentBrightness = other.CurrentBrightness;
            MatrixOptions = other.MatrixOptions.Clone();
        }
    }
}
