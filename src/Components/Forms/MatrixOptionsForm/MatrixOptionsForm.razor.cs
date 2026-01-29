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
        [Parameter] public bool OnGlobalOptionsForm { get; set; } = false;
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

        private bool ShowBrightness => Visibility.BrightnessEnabled ?? false;
        private bool ShowRows => Visibility.RowsEnabled ?? false;
        private bool ShowCols => Visibility.ColsEnabled ?? false;
        private bool ShowChainLength => Visibility.ChainLengthEnabled ?? false;
        private bool ShowParallel => Visibility.ParallelEnabled ?? false;
        private bool ShowPwmBits => Visibility.PwmBitsEnabled ?? false;
        private bool ShowPwmLsbNanoseconds => Visibility.PwmLsbNanosecondsEnabled ?? false;
        private bool ShowPwmDitherBits => Visibility.PwmDitherBitsEnabled ?? false;
        private bool ShowHardwareMapping => Visibility.HardwareMappingEnabled ?? false;
        private bool ShowDisableHardwarePulsing => Visibility.DisableHardwarePulsingEnabled ?? false;
        private bool ShowInverseColors => Visibility.InverseColorsEnabled ?? false;
        private bool ShowLedRgbSequence => Visibility.LedRgbSequenceEnabled ?? false;
        private bool ShowPixelMapperConfig => Visibility.PixelMapperConfigEnabled ?? false;
        private bool ShowPanelType => Visibility.PanelTypeEnabled ?? false;
        private bool ShowLimitRefreshRateHz => Visibility.LimitRefreshRateHzEnabled ?? false;
        private bool ShowGpioSlowdown => Visibility.GpioSlowdownEnabled ?? false;
        private bool ShowRowAddressType => Visibility.RowAddressTypeEnabled ?? false;
        private bool ShowScanMode => Visibility.ScanModeEnabled ?? false;
        private bool ShowMultiplexing => Visibility.MultiplexingEnabled ?? false;

        // Checkbox helpers for full options page
        private bool BrightnessEnabledChecked
        {
            get => Visibility.BrightnessEnabled ?? false;
            set => Visibility.BrightnessEnabled = value ? true : null;
        }
        private bool RowsEnabledChecked
        {
            get => Visibility.RowsEnabled ?? false;
            set => Visibility.RowsEnabled = value ? true : null;
        }
        private bool ColsEnabledChecked
        {
            get => Visibility.ColsEnabled ?? false;
            set => Visibility.ColsEnabled = value ? true : null;
        }
        private bool ChainLengthEnabledChecked
        {
            get => Visibility.ChainLengthEnabled ?? false;
            set => Visibility.ChainLengthEnabled = value ? true : null;
        }
        private bool ParallelEnabledChecked
        {
            get => Visibility.ParallelEnabled ?? false;
            set => Visibility.ParallelEnabled = value ? true : null;
        }
        private bool PwmBitsEnabledChecked
        {
            get => Visibility.PwmBitsEnabled ?? false;
            set => Visibility.PwmBitsEnabled = value ? true : null;
        }
        private bool PwmLsbNanosecondsEnabledChecked
        {
            get => Visibility.PwmLsbNanosecondsEnabled ?? false;
            set => Visibility.PwmLsbNanosecondsEnabled = value ? true : null;
        }
        private bool PwmDitherBitsEnabledChecked
        {
            get => Visibility.PwmDitherBitsEnabled ?? false;
            set => Visibility.PwmDitherBitsEnabled = value ? true : null;
        }
        // Wrapper for PwmDitherBits select: 0 => null (use default)
        private int PwmDitherBitsSelectValue
        {
            get => Options.PwmDitherBits ?? 0;
            set => Options.PwmDitherBits = value == 0 ? null : value;
        }
        private bool HardwareMappingEnabledChecked
        {
            get => Visibility.HardwareMappingEnabled ?? false;
            set => Visibility.HardwareMappingEnabled = value ? true : null;
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
        private bool DisableHardwarePulsingEnabledChecked
        {
            get => Visibility.DisableHardwarePulsingEnabled ?? false;
            set => Visibility.DisableHardwarePulsingEnabled = value ? true : null;
        }

        private bool InverseColorsEnabledChecked
        {
            get => Visibility.InverseColorsEnabled ?? false;
            set => Visibility.InverseColorsEnabled = value ? true : null;
        }
        private bool LedRgbSequenceEnabledChecked
        {
            get => Visibility.LedRgbSequenceEnabled ?? false;
            set => Visibility.LedRgbSequenceEnabled = value ? true : null;
        }
        private bool PixelMapperConfigEnabledChecked
        {
            get => Visibility.PixelMapperConfigEnabled ?? false;
            set => Visibility.PixelMapperConfigEnabled = value ? true : null;
        }
        private bool PanelTypeEnabledChecked
        {
            get => Visibility.PanelTypeEnabled ?? false;
            set => Visibility.PanelTypeEnabled = value ? true : null;
        }
        // Wrapper for PanelType select: empty => null (use default)
        private string PanelTypeSelectValue
        {
            get => Options.PanelType ?? string.Empty;
            set => Options.PanelType = string.IsNullOrEmpty(value) ? null : value;
        }
        private bool LimitRefreshRateHzEnabledChecked
        {
            get => Visibility.LimitRefreshRateHzEnabled ?? false;
            set => Visibility.LimitRefreshRateHzEnabled = value ? true : null;
        }
        private bool GpioSlowdownEnabledChecked
        {
            get => Visibility.GpioSlowdownEnabled ?? false;
            set => Visibility.GpioSlowdownEnabled = value ? true : null;
        }
        // Wrapper for GpioSlowdown select: empty => null (use library default, typically 1)
        private string GpioSlowdownSelectValue
        {
            get => Options.GpioSlowdown?.ToString() ?? string.Empty;
            set => Options.GpioSlowdown = string.IsNullOrEmpty(value) ? null : int.Parse(value);
        }
        private bool RowAddressTypeEnabledChecked
        {
            get => Visibility.RowAddressTypeEnabled ?? false;
            set => Visibility.RowAddressTypeEnabled = value ? true : null;
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
        private bool ScanModeEnabledChecked
        {
            get => Visibility.ScanModeEnabled ?? false;
            set => Visibility.ScanModeEnabled = value ? true : null;
        }
        private bool MultiplexingEnabledChecked
        {
            get => Visibility.MultiplexingEnabled ?? false;
            set => Visibility.MultiplexingEnabled = value ? true : null;
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