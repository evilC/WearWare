using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WearWare.Common.Media;
using WearWare.Components.Forms.EditPlayableItemForm;

namespace WearWare.Components.Forms.AddPlayableItemForm
{
    public partial class AddPlayableItemForm
    {
        [Inject] private IJSRuntime JS { get; set; } = null!;
        
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JS.InvokeVoidAsync("import", "/js/ScrollbarHider.js");
                await JS.InvokeVoidAsync("modalScrollLock.lock");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await JS.InvokeVoidAsync("modalScrollLock.unlock");
        }
        [Parameter] public EditPlayableItemFormModel? FormModel { get; set; }
        [Parameter] public IReadOnlyList<PlayableItem>? LibraryItems { get; set; }
        [Parameter] public EventCallback OnCancel { get; set; }
        [Parameter] public EventCallback<(int insertIndex, PlayableItem libItem)> OnAdd { get; set; }
        [Parameter] public int InsertIndex { get; set; }
        // ToDo: Remove
        [Parameter] public string PageTitle { get; set; } = "Playlist";

        // Called when an item is selected from the list
        private void ItemSelected(PlayableItem libItem)
        {
            if (FormModel == null)
                return;
            FormModel.OriginalItem = libItem;
            OnAdd.InvokeAsync((InsertIndex, libItem));
        }

        public async Task UnlockScrollAsync()
        {
            await JS.InvokeVoidAsync("modalScrollLock.unlock");
        }
    }
}