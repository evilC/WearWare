using Microsoft.AspNetCore.Components.Forms;
using RPiRgbLEDMatrix;
using WearWare.Services.MatrixConfig;

namespace WearWare.Components.Forms.MatrixOptionsForm
{
    public partial class MatrixOptionsForm
    {
        /// <summary> The z-index for this form </summary>
        [Parameter] public int ZIndex { get; set; } = 2010;
        [Parameter] public string? Title { get; set; }
        [Parameter] public string Action { get; set; } = "Save";
        [Parameter] public LedMatrixOptionsConfig Options { get; set; } = new LedMatrixOptionsConfig();
        [Parameter] public EventCallback<LedMatrixOptionsConfig> OnValidSubmit { get; set; }
        [Parameter] public EventCallback OnCancel { get; set; }
        // Defaults from the underlying native options - used to show cue text when
        // a nullable option is left empty.
        private readonly RGBLedMatrixOptions _defaults = new RGBLedMatrixOptions();

        private EditContext? _editContext;
        private bool _hasValidationErrors = false;

        private string ArgsPreview { get; set; } = string.Empty;

        protected override void OnParametersSet()
        {
            // Recreate the EditContext when the Options instance changes so validation is wired correctly.
            if (_editContext == null || _editContext.Model != Options)
            {
                if (_editContext != null)
                {
                    _editContext.OnValidationStateChanged -= HandleValidationStateChanged;
                    _editContext.OnFieldChanged -= HandleFieldChanged;
                }

                _editContext = new EditContext(Options);
                _editContext.OnValidationStateChanged += HandleValidationStateChanged;
                _editContext.OnFieldChanged += HandleFieldChanged;
                // initial error state
                _hasValidationErrors = _editContext.GetValidationMessages().Any();
                UpdateArgsPreview();
            }
        }

        private void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
        {
            // Validate on each field change so validation messages update while typing
            _editContext?.Validate();
            UpdateArgsPreview();
        }

        private void UpdateArgsPreview()
        {
            ArgsPreview = Options?.ToArgsString(100) ?? string.Empty;
            InvokeAsync(StateHasChanged);
        }

        private void HandleValidationStateChanged(object? sender, ValidationStateChangedEventArgs e)
        {
            if (_editContext == null) return;
            _hasValidationErrors = _editContext.GetValidationMessages().Any();
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            if (_editContext != null)
            {
                _editContext.OnValidationStateChanged -= HandleValidationStateChanged;
                _editContext.OnFieldChanged -= HandleFieldChanged;
            }
        }

        private async Task HandleOk()
        {
            if (OnCancel.HasDelegate)
            {
                await OnCancel.InvokeAsync(null);
            }
        }

        private async Task HandleValidSubmit()
        {
            if (OnValidSubmit.HasDelegate)
            {
                await OnValidSubmit.InvokeAsync(Options);
            }
        }
    }
}