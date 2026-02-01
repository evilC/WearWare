using Microsoft.AspNetCore.Components;
using WearWare.Common.Media;

namespace WearWare.Components.Shared.PlayableItemCard
{
    public partial class PlayableItemCard
    {
        [Parameter] public PlayableItem? Item { get; set; }
        [Parameter] public string? ImageSrc { get; set; }
        [Parameter] public EventCallback<PlayableItem?> ImageClicked { get; set; }
        [Parameter] public bool ShowPlayMode { get; set; } = true;

        private async Task HandleImageClick()
        {
            if (ImageClicked.HasDelegate)
            {
                await ImageClicked.InvokeAsync(Item);
            }
        }
    }
}