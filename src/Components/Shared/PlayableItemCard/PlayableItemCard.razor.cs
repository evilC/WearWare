using Microsoft.AspNetCore.Components;
using WearWare.Common.Media;
using WearWare.Components.Forms.EditPlayableItemForm;

namespace WearWare.Components.Shared.PlayableItemCard
{
    public partial class PlayableItemCard
    {
        [Parameter] public PlayableItem? Item { get; set; }
        [Parameter] public string? ImageSrc { get; set; }
        [Parameter] public EventCallback<PlayableItem?> ImageClicked { get; set; }
        [Parameter] public EditPlayableItemFormPage FormPage { get; set; }

        private async Task HandleImageClick()
        {
            if (ImageClicked.HasDelegate)
            {
                await ImageClicked.InvokeAsync(Item);
            }
        }
    }
}