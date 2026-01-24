/*
This class is used to control which options are visibile in the UI on some pages.
Only the main Matrix Options page shows all options; other pages may show only a subset.
*/
namespace WearWare.Services.MatrixConfig
{
    public class LedMatrixOptionsVisibility
    {
        // Nullable so only true values are serialized by JsonUtils (which ignores nulls).
        public bool? BrightnessEnabled { get; set; } = true;
        public bool? ChainLengthEnabled { get; set; } = null;
        public bool? ColsEnabled { get; set; } = null;
        public bool? DisableHardwarePulsingEnabled { get; set; } = null;
        public bool? GpioSlowdownEnabled { get; set; } = null;
        public bool? HardwareMappingEnabled { get; set; } = null;
        public bool? InverseColorsEnabled { get; set; } = null;
        public bool? LedRgbSequenceEnabled { get; set; } = null;
        public bool? LimitRefreshRateHzEnabled { get; set; } = null;
        public bool? MultiplexingEnabled { get; set; } = null;
        public bool? PanelTypeEnabled { get; set; } = null;
        public bool? ParallelEnabled { get; set; } = null;
        public bool? PixelMapperConfigEnabled { get; set; } = null;
        public bool? PwmBitsEnabled { get; set; } = null;
        public bool? PwmDitherBitsEnabled { get; set; } = null;
        public bool? PwmLsbNanosecondsEnabled { get; set; } = null;
        public bool? RowAddressTypeEnabled { get; set; } = null;
        public bool? RowsEnabled { get; set; } = null;
        public bool? ScanModeEnabled { get; set; } = null;

        public LedMatrixOptionsVisibility Clone()
        {
            return (LedMatrixOptionsVisibility)this.MemberwiseClone();
        }
    }
}
