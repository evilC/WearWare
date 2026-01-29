using Microsoft.AspNetCore.Components;
using WearWare.Common.Media;

namespace WearWare.Components.Pages.Shared.PlaylistItem
{
    public partial class PlaylistItem
    {
        [Parameter] public PlayableItem? Item { get; set; }
        [Parameter] public string? ImageSrc { get; set; }
        [Parameter] public EventCallback<PlayableItem?> ImageClicked { get; set; }

        private async Task HandleImageClick()
        {
            if (ImageClicked.HasDelegate)
            {
                await ImageClicked.InvokeAsync(Item);
            }
        }
    }
}