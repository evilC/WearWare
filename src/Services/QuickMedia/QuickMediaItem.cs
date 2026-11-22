using System.ComponentModel.DataAnnotations;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Services.QuickMedia
{
    /// <summary>
    /// Represents a quick media item - a simplified media item for quick button-triggered playback.
    /// Unlike PlayableItem (used by Playlist), QuickMediaItem does not have an "Enabled" field
    /// since quick media items are always available for playback when assigned to a button.
    /// </summary>
    public class QuickMediaItem
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

        public QuickMediaItem(string name,
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
        /// Converts this QuickMediaItem to a PlayableItem (for use with media player).
        /// </summary>
        public PlayableItem ToPlayableItem()
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
        /// Creates a QuickMediaItem from a PlayableItem, discarding the Enabled property.
        /// </summary>
        public static QuickMediaItem FromPlayableItem(PlayableItem item)
        {
            return new QuickMediaItem(
                item.Name,
                item.ParentFolder,
                item.MediaType,
                item.SourceFileName,
                item.PlayMode,
                item.PlayModeValue,
                item.RelativeBrightness
            );
        }
    }
}
