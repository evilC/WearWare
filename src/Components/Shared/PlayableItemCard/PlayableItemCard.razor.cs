using WearWare.Components.Forms.EditPlayableItemForm;

namespace WearWare.Components.Shared.PlayableItemCard
{
    public partial class PlayableItemCard
    {
        [Parameter] public PlayableItem? Item { get; set; }
        [Parameter] public string? ImageSrc { get; set; }
        
        [Parameter] public EditPlayableItemFormPage FormPage { get; set; }
        [Parameter] public EditPlayableItemFormMode FormMode { get; set; }

        private string RenderPlayMode()
        {
            if (Item == null) return "";
            var str = Item.PlayMode.ToString();
            if (Item.PlayMode == PlayMode.Forever) return str;
            str += $": {Item.PlayModeValue}";
            return Item.PlayMode == PlayMode.Loop ? $"{str}x" : $"{str}s";
        }
    }
}