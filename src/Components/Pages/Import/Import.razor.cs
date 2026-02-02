using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using WearWare.Common;
using WearWare.Common.Media;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Config;
using WearWare.Services.Import;
using WearWare.Services.MatrixConfig;
using WearWare.Utils;

namespace WearWare.Components.Pages.Import
{
    public partial class Import
    {
        [Inject] private ImportService ImportService { get; set; } = null!;
        [Inject] private MatrixConfigService MatrixConfigService { get; set; } = null!;
        [Inject] private HttpClient Http { get; set; } = null!;
        // ToDo: Used for confirmations - replace with Blazor component?
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private NavigationManager Navigation { get; set; } = null!;
        
        EditPlayableItemFormModel? _editFormModel = null;
        private List<PlayableItem>? importItems;

        private EditPlayableItemForm? editFormRef;
        private string inputId = "fileInput_" + Guid.NewGuid().ToString("N");

        protected override void OnInitialized()
        {
            importItems = ImportService.GetImportItems();
            ImportService.StateChanged += OnStateChanged;
        }

        private void OnStateChanged()
        {
            importItems = ImportService.GetImportItems();
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            ImportService.StateChanged -= OnStateChanged;
        }

        /// <summary>
        /// Show the edit form for the selected incoming file.
        /// </summary>
        private async Task ShowForm(PlayableItem item)
        {

            var editFormModel = new EditPlayableItemFormModel
            {
                ImageUrl = $"/incoming-media/{item.SourceFileName}",
                FormMode = EditPlayableItemFormMode.Add,
                FormPage = EditPlayableItemFormPage.Import,
                OriginalItem = item,
                UpdatedItem = item.Clone(),
            };
            _editFormModel = editFormModel;
            // ToDo: Why is this called?
            await InvokeAsync(StateHasChanged);
        }

        private async Task ConfirmDelete(PlayableItem item)
        {
            var ok = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete incoming file '{item.SourceFileName}'?");
            if (ok)
            {
                ImportService.DeleteIncomingFile(item.SourceFileName);
            }
        }

        /// <summary>
        /// Called when the edit form is submitted in the Import page.
        /// </summary>
        private async Task OnSaveImportItem(EditPlayableItemFormModel formModel)
        {
            _editFormModel = null;
            await ImportService.OnEditFormSubmit(formModel);
            // clear state
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when the edit form is cancelled or closed.
        /// </summary>
        private async Task OnEditFormCancel()
        {
            _editFormModel = null;
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when a file is selected for upload.
        /// </summary>
        private async Task HandleFileSelected(InputFileChangeEventArgs e)
        {
            var file = e.File;
            if (file == null)
                return;

            var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream(long.MaxValue);
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            content.Add(streamContent, "file", file.Name);

            try
            {
                var uploadUrl = Navigation.BaseUri.TrimEnd('/') + "/incoming-media/upload";
                var resp = await Http.PostAsync(uploadUrl, content);
                if (!resp.IsSuccessStatusCode)
                {
                    var msg = await resp.Content.ReadAsStringAsync();
                    await JSRuntime.InvokeVoidAsync("alert", $"Upload failed: {msg}");
                }
                else
                {
                    // Refresh local list (endpoint will also notify ImportService)
                    importItems = ImportService.GetImportItems();
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (Exception ex)
            {
                await JSRuntime.InvokeVoidAsync("alert", $"Upload error: {ex.Message}");
            }
        }

        private async Task TriggerFileInput()
        {
            // Use a small eval to click the hidden file input by id
            try
            {
                await JSRuntime.InvokeVoidAsync("eval", $"document.getElementById('{inputId}').click();");
            }
            catch
            {
                // Ignore JS errors
            }
        }
    }
}