using System.ComponentModel.DataAnnotations;
using WearWare.Utils;

namespace WearWare.Components.Forms.AddCopyPlaylistForm
{
    public class AddCopyPlaylistFormModel
    {
        public AddCopyPlaylistMode Mode { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [RegularExpression($"^[{FilenameValidator.AllowedPattern}]+$", ErrorMessage = "Name must only contain letters, numbers, dashes, or underscores.")]
        public string NewName { get; set; } = default!;
        public string OldName { get; set; } = default!;
    }
}