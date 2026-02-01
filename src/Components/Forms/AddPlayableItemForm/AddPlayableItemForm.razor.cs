using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WearWare.Common.Media;
using WearWare.Components.Forms.EditPlayableItemForm;

namespace WearWare.Components.Forms.AddPlayableItemForm
{
    public partial class AddPlayableItemForm
    {
        [Inject] private IJSRuntime JS { get; set; } = null!;
        [Inject] private ILogger<AddPlayableItemForm> _logger { get; set; } = null!;
        private string _logTag = "AddPlayableItemForm";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
        }
        [Parameter] public EditPlayableItemFormModel? FormModel { get; set; }
        [Parameter] public IReadOnlyList<PlayableItem>? LibraryItems { get; set; }
        [Parameter] public EventCallback OnCancel { get; set; }
        [Parameter] public EventCallback<EditPlayableItemFormModel> OnAdd { get; set; }
        // ToDo: Remove
        [Parameter] public string PageTitle { get; set; } = "Playlist";

        // Called when an item is selected from the list
        private void ItemSelected(PlayableItem libItem)
        {
            if (FormModel == null)
            {
                _logger.LogError($"{_logTag}: ItemSelected called but FormModel is null");
                return;
            }
            FormModel.OriginalItem = libItem;
            FormModel.UpdatedItem = libItem.Clone();
            OnAdd.InvokeAsync(FormModel);
        }
    }
}