/*
This class is a wrapper around RGBLedMatrixOptions to facilitate various things:
1) Validation via DataAnnotations
2) All fields are nullable, so that we can distinguish between "not set" and "set to default value"
3) Conversion to command-line argument string for led-image-viewer
4) Cloning capability - We want to be able to take a copy of the "Global" settings...
    ... and modify them for a specific animation without changing the global settings.
*/
using System.ComponentModel.DataAnnotations;
using RPiRgbLEDMatrix;
using WearWare.Common;

namespace WearWare.Services.MatrixConfig
{
    public class LedMatrixOptionsConfig : IValidatableObject
    {
        [Range(1, 100, ErrorMessage = "Must be between 1 and 100.")]
        public int? Brightness { get; set; }

        [Range(1, 3, ErrorMessage = "Must be between 1 and 3.")]
        public int? ChainLength { get; set; }

        [Range(1, 128, ErrorMessage = "Must be between 1 and 128.")]
        public int? Cols { get; set; }

        public bool? DisableHardwarePulsing { get; set; }

        // No validation needed, uses a DDL
        public int? GpioSlowdown { get; set; }

        public string? HardwareMapping { get; set; }

        public bool? InverseColors { get; set; }

        public string? LedRgbSequence { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Must be a positive integer.")]
        public int? LimitRefreshRateHz { get; set; }

        public string? Multiplexing { get; set; }

        public string? PanelType { get; set; }

        [Range(1, 3, ErrorMessage = "Must be between 1 and 3.")]
        public int? Parallel { get; set; }

        public string? PixelMapperConfig { get; set; }

        [Range(1, 11, ErrorMessage = "Must be between 1 and 11.")]
        public int? PwmBits { get; set; }

        // No validation needed, uses a DDL
        public int? PwmDitherBits { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Must be >= 0.")]
        public int? PwmLsbNanoseconds { get; set; }

        // No validation needed, uses a DDL
        public int? RowAddressType { get; set; }

        [Range(1, 128, ErrorMessage = "Must be between 1 and 128.")]
        public int? Rows { get; set; }

        public string? ScanMode { get; set; }

        /// <summary>
        /// Converts the current options to an RGBLedMatrixOptions instance.
        /// </summary>
        /// <returns>RGBLedMatrixOptions instance.</returns>
        public RGBLedMatrixOptions ToRGBLedMatrixOptions()
        {
            var opts = new RGBLedMatrixOptions();
            // Assign options in alphabetical order by property name
            if (Brightness.HasValue) opts.Brightness = Brightness.Value;
            if (ChainLength.HasValue) opts.ChainLength = ChainLength.Value;
            if (Cols.HasValue) opts.Cols = Cols.Value;
            if (DisableHardwarePulsing.HasValue) opts.DisableHardwarePulsing = DisableHardwarePulsing.Value;
            if (GpioSlowdown.HasValue) opts.GpioSlowdown = GpioSlowdown.Value;
            if (HardwareMapping != null) opts.HardwareMapping = HardwareMapping;
            if (InverseColors.HasValue) opts.InverseColors = InverseColors.Value;
            if (LedRgbSequence != null) opts.LedRgbSequence = LedRgbSequence;
            if (LimitRefreshRateHz.HasValue) opts.LimitRefreshRateHz = LimitRefreshRateHz.Value;
            if (Multiplexing != null && Enum.TryParse<Multiplexing>(Multiplexing, out var multiplexingVal)) opts.Multiplexing = multiplexingVal;
            if (PanelType != null) opts.PanelType = PanelType;
            if (Parallel.HasValue) opts.Parallel = Parallel.Value;
            if (PixelMapperConfig != null) opts.PixelMapperConfig = PixelMapperConfig;
            if (PwmBits.HasValue) opts.PwmBits = PwmBits.Value;
            if (PwmDitherBits.HasValue) opts.PwmDitherBits = PwmDitherBits.Value;
            if (PwmLsbNanoseconds.HasValue) opts.PwmLsbNanoseconds = PwmLsbNanoseconds.Value;
            if (RowAddressType.HasValue) opts.RowAddressType = RowAddressType.Value;
            if (Rows.HasValue) opts.Rows = Rows.Value;
            if (ScanMode != null && Enum.TryParse<ScanModes>(ScanMode, out var scanModeVal)) opts.ScanMode = scanModeVal;

            return opts;

        }

        /// <summary>
        /// Converts the current options to a command-line argument string for led-image-viewer.
        /// </summary>
        /// <returns>Command-line argument string.</returns>
        public string ToArgsString(int relativeBrightness)
        {
            var args = new List<string>();
            if (Brightness.HasValue || relativeBrightness != 100)
                args.Add($"--led-brightness={BrightnessCalculator.CalculateAbsoluteBrightness(Brightness ?? 100, relativeBrightness)}");
            if (ChainLength.HasValue) args.Add($"--led-chain={ChainLength}");
            if (Cols.HasValue) args.Add($"--led-cols={Cols}");
            if (DisableHardwarePulsing == true) args.Add("--led-no-hardware-pulse");
            if (GpioSlowdown.HasValue) args.Add($"--led-slowdown-gpio={GpioSlowdown}");
            if (HardwareMapping != null) args.Add($"--led-hardware-mapping={HardwareMapping}");
            if (InverseColors == true) args.Add("--led-inverse");
            if (LedRgbSequence != null) args.Add($"--led-rgb-sequence={LedRgbSequence}");
            if (LimitRefreshRateHz.HasValue) args.Add($"--led-limit-refresh={LimitRefreshRateHz}");
            if (Multiplexing != null) args.Add($"--led-multiplexing={Multiplexing}");
            if (PanelType != null) args.Add($"--led-panel-type={PanelType}");
            if (Parallel.HasValue) args.Add($"--led-parallel={Parallel}");
            if (PixelMapperConfig != null) args.Add($"--led-pixel-mapper={PixelMapperConfig}");
            if (PwmBits.HasValue) args.Add($"--led-pwm-bits={PwmBits}");
            if (PwmDitherBits.HasValue) args.Add($"--led-pwm-dither-bits={PwmDitherBits}");
            if (PwmLsbNanoseconds.HasValue) args.Add($"--led-pwm-lsb-nanoseconds={PwmLsbNanoseconds}");
            if (RowAddressType.HasValue) args.Add($"--led-row-addr-type={RowAddressType}");
            if (Rows.HasValue) args.Add($"--led-rows={Rows}");
            if (ScanMode != null) args.Add($"--led-scan-mode={ScanMode}");
            return string.Join(" ", args);
        }

        // No validation for free-text fields: blank means "use default".
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield break;
        }

        /// <summary>
        /// Returns a deep copy of this instance.
        /// </summary>
        public LedMatrixOptionsConfig Clone()
        {
            return new LedMatrixOptionsConfig
            {
                Brightness = this.Brightness,
                ChainLength = this.ChainLength,
                Cols = this.Cols,
                DisableHardwarePulsing = this.DisableHardwarePulsing,
                GpioSlowdown = this.GpioSlowdown,
                HardwareMapping = this.HardwareMapping,
                InverseColors = this.InverseColors,
                LedRgbSequence = this.LedRgbSequence,
                LimitRefreshRateHz = this.LimitRefreshRateHz,
                Multiplexing = this.Multiplexing,
                PanelType = this.PanelType,
                Parallel = this.Parallel,
                PixelMapperConfig = this.PixelMapperConfig,
                PwmBits = this.PwmBits,
                PwmDitherBits = this.PwmDitherBits,
                PwmLsbNanoseconds = this.PwmLsbNanoseconds,
                RowAddressType = this.RowAddressType,
                Rows = this.Rows,
                ScanMode = this.ScanMode,
            };
        }

        public bool IsEqual(LedMatrixOptionsConfig other)
        {
            if (other == null)
                return false;

            return
                    Brightness == other.Brightness &&
                    ChainLength == other.ChainLength &&
                    Cols == other.Cols &&
                    DisableHardwarePulsing == other.DisableHardwarePulsing &&
                    GpioSlowdown == other.GpioSlowdown &&
                    HardwareMapping == other.HardwareMapping &&
                    InverseColors == other.InverseColors &&
                    LedRgbSequence == other.LedRgbSequence &&
                    LimitRefreshRateHz == other.LimitRefreshRateHz &&
                    Multiplexing == other.Multiplexing &&
                    PanelType == other.PanelType &&
                    Parallel == other.Parallel &&
                    PixelMapperConfig == other.PixelMapperConfig &&
                    PwmBits == other.PwmBits &&
                    PwmDitherBits == other.PwmDitherBits &&
                    PwmLsbNanoseconds == other.PwmLsbNanoseconds &&
                    RowAddressType == other.RowAddressType &&
                    Rows == other.Rows &&
                    ScanMode == other.ScanMode;
        }   
    }
}
