using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using RPiRgbLEDMatrix;
using WearWare.Common;
using WearWare.Services.MatrixConfig;

namespace WearWare.Components.Forms.MatrixOptionsForm
{
    public partial class MatrixOptionsForm
    {
        [Parameter] public string Action { get; set; } = "Save";
        [Parameter] public string? Title { get; set; }
        [Parameter] public LedMatrixOptionsConfig Options { get; set; } = new LedMatrixOptionsConfig();
        [Parameter] public LedMatrixOptionsVisibility Visibility { get; set; } = new LedMatrixOptionsVisibility();
        [Parameter] public int RelativeBrightness { get; set; }
        // True if we are on the Global version of the Matrix Options form
        // Only on this page are all fields shown, and checkboxes rendered to enable/disable each option
        // Also when on the Global form, the Relative and Actual Brightness fields are not shown
        [Parameter] public bool OnGlobalOptionsForm { get; set; }
        // True if we are on the Reconvert All form
        // Relative / Actual Brightness fields are not shown
        [Parameter] public bool HideRelativeBrightness { get; set; }
        [Parameter] public EventCallback<LedMatrixOptionsConfig> OnValidSubmit { get; set; }
        [Parameter] public EventCallback OnCancel { get; set; }
        // Defaults from the underlying native options - used to show cue text when
        // a nullable option is left empty.
        private readonly RGBLedMatrixOptions _defaults = new RGBLedMatrixOptions();

        private EditContext? _editContext;
        private bool _hasValidationErrors = false;

        private string ArgsPreview { get; set; } = string.Empty;

        // Helper properties for rendering
        private int ActualBrightness => BrightnessCalculator.CalculateAbsoluteBrightness(Options?.Brightness ?? _defaults.Brightness, RelativeBrightness);
        // Wrapper for PwmDitherBits select: 0 => null (use default)
        private int PwmDitherBitsSelectValue
        {
            get => Options.PwmDitherBits ?? 0;
            set => Options.PwmDitherBits = value == 0 ? null : value;
        }
        
        // Wrapper for HardwareMapping select: empty => null (use default)
        private string HardwareMappingSelectValue
        {
            get => Options.HardwareMapping ?? string.Empty;
            set => Options.HardwareMapping = string.IsNullOrEmpty(value) ? null : value;
        }
        // Wrapper for LedRgbSequence select: empty => null (use default)
        private string LedRgbSequenceSelectValue
        {
            get => Options.LedRgbSequence ?? string.Empty;
            set => Options.LedRgbSequence = string.IsNullOrEmpty(value) ? null : value;
        }
        
        // Wrapper for PanelType select: empty => null (use default)
        private string PanelTypeSelectValue
        {
            get => Options.PanelType ?? string.Empty;
            set => Options.PanelType = string.IsNullOrEmpty(value) ? null : value;
        }
        
        // Wrapper for GpioSlowdown select: empty => null (use library default, typically 1)
        private string GpioSlowdownSelectValue
        {
            get => Options.GpioSlowdown?.ToString() ?? string.Empty;
            set => Options.GpioSlowdown = string.IsNullOrEmpty(value) ? null : int.Parse(value);
        }
        
        // Wrapper for select UI: 0 represents 'default' and stores null in Options.RowAddressType
        private int RowAddressTypeSelectValue
        {
            get => Options.RowAddressType ?? 0;
            set => Options.RowAddressType = value == 0 ? null : value;
        }
        // Wrapper for Multiplexing select: store numeric string or null for default
        private int MultiplexingSelectValue
        {
            get => int.TryParse(Options.Multiplexing, out var v) ? v : 0;
            set => Options.Multiplexing = value == 0 ? null : value.ToString();
        }
        

        // Wrapper for ScanMode select: 0 => default (null), otherwise store enum name
        private int ScanModeSelectValue
        {
            get => (Options.ScanMode != null && Enum.TryParse<ScanModes>(Options.ScanMode, out var v)) ? (int)v : 0;
            set => Options.ScanMode = value == 0 ? null : Enum.GetName(typeof(ScanModes), value);
        }

        private bool DisableHardwarePulsingChecked
        {
            get => Options.DisableHardwarePulsing ?? false;
            set => Options.DisableHardwarePulsing = value ? true : null;
        }

        private bool InverseColorsChecked
        {
            get => Options.InverseColors ?? false;
            set => Options.InverseColors = value ? true : null;
        }

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
            ArgsPreview = Options?.ToArgsString(RelativeBrightness) ?? string.Empty;
            InvokeAsync(StateHasChanged);
        }

        private void HandleValidationStateChanged(object? sender, ValidationStateChangedEventArgs e)
        {
            if (_editContext == null) return;
            _hasValidationErrors = _editContext.GetValidationMessages().Any();
            InvokeAsync(StateHasChanged);
        }

        private bool FieldHasErrors(string propertyName)
        {
            if (_editContext == null) return false;
            var fi = new FieldIdentifier(Options, propertyName);
            return _editContext.GetValidationMessages(fi).Any();
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