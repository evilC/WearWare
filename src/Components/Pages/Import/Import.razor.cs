using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using WearWare.Common;
using WearWare.Common.Media;
using WearWare.Components.Forms;
using WearWare.Config;
using WearWare.Services.Import;
using WearWare.Services.MatrixConfig;
using WearWare.Utils;

namespace WearWare.Components.Pages.Import
{
    public partial class Import
    {
        [Inject]
        private ImportService ImportService { get; set; } = null!;
        [Inject]
        private MatrixConfigService MatrixConfigService { get; set; } = null!;
        [Inject]
        private HttpClient Http { get; set; } = null!;
        // ToDo: Used for confirmations - replace with Blazor component?
        [Inject]
        private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject]
        private NavigationManager Navigation { get; set; } = null!;
        
        private List<string>? importFiles;
        private string? selectedFileName;
        private bool showEditDialog = false;
        private string? pendingNewFileName;

        private PlayableItem? _editingItem;
        private EditPlayableItemForm? editFormRef;
        private string inputId = "fileInput_" + Guid.NewGuid().ToString("N");

        protected override void OnInitialized()
        {
            importFiles = ImportService.GetImportFiles();
            ImportService.StateChanged += OnStateChanged;
        }

        private void OnStateChanged()
        {
            importFiles = ImportService.GetImportFiles();
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            ImportService.StateChanged -= OnStateChanged;
        }

        /// <summary>
        /// Show the edit form for the selected incoming file.
        /// </summary>
        private async Task ShowForm(string fileName)
        {
            selectedFileName = fileName;
            // Prepare an editing PlayableItem immediately (ImportForm UI moved into EditPlayableItemForm)
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(selectedFileName)) ?? MediaType.IMAGE;
            var baseName = Path.GetFileNameWithoutExtension(selectedFileName);
            var sanitized = FilenameValidator.Sanitize(baseName);
            var baseBrightness = MatrixConfigService.CloneOptions().Brightness ?? 100;
            var actual = BrightnessCalculator.CalculateAbsoluteBrightness(baseBrightness, 100);
            _editingItem = new PlayableItem(
                sanitized,
                PathConfig.LibraryFolder,
                mediaType,
                selectedFileName,
                PlayMode.FOREVER,
                1,
                100,
                actual,
                MatrixConfigService.CloneOptions()
            );
            pendingNewFileName = sanitized;
            showEditDialog = true;
            await InvokeAsync(StateHasChanged);
        }

        private async Task ConfirmDelete(string fileName)
        {
            var ok = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete incoming file '{fileName}'?");
            if (ok)
            {
                ImportService.DeleteIncomingFile(fileName);
            }
        }


        private async Task OnSaveImportItem((int editingIndex, PlayableItem updatedItem, PlayableItemFormMode formMode) args)
        {
            showEditDialog = false;
            if (pendingNewFileName != null && selectedFileName != null)
            {
                await ImportService.OnEditFormSubmit(selectedFileName, args.updatedItem.Name, args.updatedItem.RelativeBrightness, args.updatedItem.MatrixOptions);
            }
            // clear state
            pendingNewFileName = null;
            selectedFileName = null;
            _editingItem = null;
            if (editFormRef is not null)
                await editFormRef.UnlockScrollAsync();
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when the edit form is cancelled or closed.
        /// </summary>
        private async Task OnEditFormCancel()
        {
            // Close the edit form and return to the import list without importing
            showEditDialog = false;
            pendingNewFileName = null;
            selectedFileName = null;
            _editingItem = null;
            if (editFormRef is not null)
                await editFormRef.UnlockScrollAsync();
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
                    importFiles = ImportService.GetImportFiles();
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