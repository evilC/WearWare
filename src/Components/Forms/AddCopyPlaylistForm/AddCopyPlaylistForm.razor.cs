using Microsoft.AspNetCore.Components.Forms;
using WearWare.Services.Playlist;

namespace WearWare.Components.Forms.AddCopyPlaylistForm
{
    public partial class AddCopyPlaylistForm
    {
        [Parameter] public AddCopyPlaylistFormModel Model { get; set; } = default!;

        [Inject] private PlaylistService PlaylistService { get; set; } = null!;

        [Parameter] public EventCallback<AddCopyPlaylistFormModel> OnSubmit { get; set; }
        [Parameter] public EventCallback OnCancel { get; set; }

        private EditContext? _editContext;
        private ValidationMessageStore? _messageStore;

        protected override void OnParametersSet()
        {
            if (_editContext == null || _editContext.Model != Model)
            {
                if (_editContext != null)
                {
                    _editContext.OnFieldChanged -= EditContext_OnFieldChanged;
                }

                _editContext = new EditContext(Model);
                _messageStore = new ValidationMessageStore(_editContext);
                _editContext.OnFieldChanged += EditContext_OnFieldChanged;
            }
        }

        private void EditContext_OnFieldChanged(object? sender, FieldChangedEventArgs e)
        {
            if (e.FieldIdentifier.FieldName != nameof(Model.NewName))
                return;

            _messageStore?.Clear(e.FieldIdentifier);

            // Run data annotations validation for the field
            _editContext?.Validate();

            // Live uniqueness check
            if (!string.IsNullOrWhiteSpace(Model.NewName)
                && PlaylistService.GetPlaylistNames().Any(n => string.Equals(n, Model.NewName, StringComparison.OrdinalIgnoreCase)))
            {
                _messageStore?.Add(e.FieldIdentifier, "A playlist with that name already exists.");
            }

            _editContext?.NotifyValidationStateChanged();
        }

        private Task OnCancelClicked()
        {
            return OnCancel.InvokeAsync(null);
        }

        private async Task OnSubmitClicked()
        {
            _messageStore?.Clear();

            // Validate data annotations
            var valid = _editContext?.Validate() ?? true;
            if (!valid)
            {
                return;
            }

            // Uniqueness check handled here in the form (service-aware)
            if (PlaylistService.GetPlaylistNames().Any(n => string.Equals(n, Model.NewName, StringComparison.OrdinalIgnoreCase)))
            {
                _messageStore?.Add(new FieldIdentifier(Model, nameof(Model.NewName)), "A playlist with that name already exists.");
                _editContext?.NotifyValidationStateChanged();
                return;
            }

            await OnSubmit.InvokeAsync(Model);
        }
    }
}