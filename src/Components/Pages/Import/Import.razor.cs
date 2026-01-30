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
        private List<string>? importFiles;

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

        private async Task UnlockScrollbar(){
            if (editFormRef is not null)
                await editFormRef.UnlockScrollAsync();
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
            // selectedFileName = fileName;
            // Prepare an editing PlayableItem immediately (ImportForm UI moved into EditPlayableItemForm)
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(fileName)) ?? MediaType.IMAGE;
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var sanitized = FilenameValidator.Sanitize(baseName);
            var baseBrightness = MatrixConfigService.CloneOptions().Brightness ?? 100;
            var actual = BrightnessCalculator.CalculateAbsoluteBrightness(baseBrightness, 100);
            var item = new PlayableItem(
                sanitized,
                PathConfig.LibraryFolder,
                MediaTypeMappings.GetMediaType(Path.GetExtension(fileName)) ?? MediaType.IMAGE,
                fileName,
                PlayMode.Forever,
                1,
                100,
                actual,
                MatrixConfigService.CloneOptions()
            );
            var editFormModel = new EditPlayableItemFormModel
            {
                ImageUrl = $"/incoming-media/{fileName}",
                FormMode = EditPlayableItemFormMode.Add,
                FormPage = EditPlayableItemFormPage.Import,
                UpdatedItem = item,
            };
            editFormModel.UpdatedItem.Name = sanitized;
            _editFormModel = editFormModel;
            // pendingNewFileName = sanitized;
            // showEditDialog = true;
            // ToDo: Why is this called?
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

        /// <summary>
        /// Called when the edit form is submitted in the Import page.
        /// </summary>
        private async Task OnSaveImportItem(EditPlayableItemFormModel formModel)
        {
            _editFormModel = null;
            await ImportService.OnEditFormSubmit(formModel);
            // clear state
            await UnlockScrollbar();
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when the edit form is cancelled or closed.
        /// </summary>
        private async Task OnEditFormCancel()
        {
            _editFormModel = null;
            await UnlockScrollbar();
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