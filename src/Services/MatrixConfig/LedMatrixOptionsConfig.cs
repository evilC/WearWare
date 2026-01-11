using System.Collections.Generic;
// validation removed per user request
using System.ComponentModel.DataAnnotations;
using RPiRgbLEDMatrix;

namespace WearWare.Services.MatrixConfig
{
    public class LedMatrixOptionsConfig : IValidatableObject
    {
        [Range(1, 100, ErrorMessage = "Must be between 1 and 100.")]
        public int? Brightness { get; set; }
        public bool? BrightnessEnabled { get; set; } = true;

        [Range(1, int.MaxValue, ErrorMessage = "Must be a positive integer.")]
        public int? ChainLength { get; set; }
        public bool? ChainLengthEnabled { get; set; } = null;

        [Range(1, int.MaxValue, ErrorMessage = "Must be a positive integer.")]
        public int? Cols { get; set; }
        public bool? ColsEnabled { get; set; } = null;

        public bool? DisableHardwarePulsing { get; set; }
        public bool? DisableHardwarePulsingEnabled { get; set; } = null;

        public int? GpioSlowdown { get; set; }
        public bool? GpioSlowdownEnabled { get; set; } = null;

        public string? HardwareMapping { get; set; }
        public bool? HardwareMappingEnabled { get; set; } = null;

        public bool? InverseColors { get; set; }
        public bool? InverseColorsEnabled { get; set; } = null;

        public string? LedRgbSequence { get; set; }
        public bool? LedRgbSequenceEnabled { get; set; } = null;

        [Range(1, int.MaxValue, ErrorMessage = "Must be a positive integer.")]
        public int? LimitRefreshRateHz { get; set; }
        public bool? LimitRefreshRateHzEnabled { get; set; } = null;

        public string? Multiplexing { get; set; }
        public bool? MultiplexingEnabled { get; set; } = null;

        public string? PanelType { get; set; }
        public bool? PanelTypeEnabled { get; set; } = null;

        [Range(1, 3, ErrorMessage = "Must be between 1 and 3.")]
        public int? Parallel { get; set; }
        public bool? ParallelEnabled { get; set; } = null;

        public string? PixelMapperConfig { get; set; }
        public bool? PixelMapperConfigEnabled { get; set; } = null;

        [Range(1, 11, ErrorMessage = "Must be between 1 and 11.")]
        public int? PwmBits { get; set; }
        public bool? PwmBitsEnabled { get; set; } = null;

        public int? PwmDitherBits { get; set; }
        public bool? PwmDitherBitsEnabled { get; set; } = null;

        [Range(0, int.MaxValue, ErrorMessage = "Must be >= 0.")]
        public int? PwmLsbNanoseconds { get; set; }
        public bool? PwmLsbNanosecondsEnabled { get; set; } = null;

        public int? RowAddressType { get; set; }
        public bool? RowAddressTypeEnabled { get; set; } = null;

        [Range(1, int.MaxValue, ErrorMessage = "Must be a positive integer.")]
        public int? Rows { get; set; }
        public bool? RowsEnabled { get; set; } = null;

        public string? ScanMode { get; set; }
        public bool? ScanModeEnabled { get; set; } = null;


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
        public string ToArgsString()
        {
            var args = new List<string>();
            // Add args in alphabetical order by property name
            if (Brightness.HasValue) args.Add($"--led-brightness={Brightness}");
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
                BrightnessEnabled = this.BrightnessEnabled,

                ChainLength = this.ChainLength,
                ChainLengthEnabled = this.ChainLengthEnabled,

                Cols = this.Cols,
                ColsEnabled = this.ColsEnabled,

                DisableHardwarePulsing = this.DisableHardwarePulsing,
                DisableHardwarePulsingEnabled = this.DisableHardwarePulsingEnabled,

                GpioSlowdown = this.GpioSlowdown,
                GpioSlowdownEnabled = this.GpioSlowdownEnabled,

                HardwareMapping = this.HardwareMapping,
                HardwareMappingEnabled = this.HardwareMappingEnabled,

                InverseColors = this.InverseColors,
                InverseColorsEnabled = this.InverseColorsEnabled,

                LedRgbSequence = this.LedRgbSequence,
                LedRgbSequenceEnabled = this.LedRgbSequenceEnabled,

                LimitRefreshRateHz = this.LimitRefreshRateHz,
                LimitRefreshRateHzEnabled = this.LimitRefreshRateHzEnabled,

                Multiplexing = this.Multiplexing,
                MultiplexingEnabled = this.MultiplexingEnabled,

                PanelType = this.PanelType,
                PanelTypeEnabled = this.PanelTypeEnabled,

                Parallel = this.Parallel,
                ParallelEnabled = this.ParallelEnabled,

                PixelMapperConfig = this.PixelMapperConfig,
                PixelMapperConfigEnabled = this.PixelMapperConfigEnabled,

                PwmBits = this.PwmBits,
                PwmBitsEnabled = this.PwmBitsEnabled,

                PwmDitherBits = this.PwmDitherBits,
                PwmDitherBitsEnabled = this.PwmDitherBitsEnabled,

                PwmLsbNanoseconds = this.PwmLsbNanoseconds,
                PwmLsbNanosecondsEnabled = this.PwmLsbNanosecondsEnabled,

                RowAddressType = this.RowAddressType,
                RowAddressTypeEnabled = this.RowAddressTypeEnabled,

                Rows = this.Rows,
                RowsEnabled = this.RowsEnabled,

                ScanMode = this.ScanMode,
                ScanModeEnabled = this.ScanModeEnabled,

                
            };
        }

    }
}
