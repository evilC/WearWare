using System.ComponentModel.DataAnnotations;
using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Common.Media
{
    public class PlayableItem
    {
        [Required]
        [RegularExpression($"^[{FilenameValidator.AllowedPattern}]+$", ErrorMessage = "Name must only contain letters, numbers, dashes, or underscores.")]
        public string Name { get; set; }
        public MediaType MediaType { get; init; }
        public string SourceFileName { get; init; }
        [Range(1, 100, ErrorMessage = "Must be between 1 and 100.")]
        public int RelativeBrightness { get; set; } = 100;
        public PlayMode PlayMode { get; set; } = PlayMode.Loop;
        public int PlayModeValue { get; set; } = 1;
        public string ParentFolder { get; set; }
        public bool Enabled { get; set; } = true;

        public PlayableItem(string name, 
            string parentFolder, 
            MediaType mediaType, 
            string sourceFileName, 
            PlayMode playMode, 
            int playModeValue, 
            int relativeBrightness)
        {
            Name = name;
            MediaType = mediaType;
            SourceFileName = sourceFileName;
            ParentFolder = parentFolder;
            PlayMode = playMode;
            PlayModeValue = playModeValue;
            RelativeBrightness = relativeBrightness;
        }

        public string GetFseqFilePath()
        {
            return Path.Combine(PathConfig.Root, ParentFolder, $"{Name}.fseq");
        }

        public string GetSourceFilePath()
        {
            return Path.Combine(PathConfig.Root, ParentFolder, SourceFileName);
        }

        /// <summary>
        /// Creates a deep clone of this PlayableItem.
        /// </summary>
        public PlayableItem Clone()
        {
            return new PlayableItem(
                Name,
                ParentFolder,
                MediaType,
                SourceFileName,
                PlayMode,
                PlayModeValue,
                RelativeBrightness
            );
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
        }
    }
}
