using System.Text.Json.Serialization;

namespace WearWare.Architecture.Button
{
    public sealed class QuickMediaButtonsConfig
    {
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = "PiGpio";

        [JsonPropertyName("buttonPins")]
        public int[]? ButtonPins { get; set; } = [];

        [JsonPropertyName("excludeI2cPins")]
        public bool ExcludeI2cPins { get; set; }

        [JsonPropertyName("interruptPins")]
        public int?[]? InterruptPins { get; set; }
    }
}
