using System.Text.Json.Serialization;

namespace WearWare.Services.Button
{
    public sealed class ButtonFunctionMapping
    {
        [JsonPropertyName("pinName")]
        public string PinName { get; set; } = "";

        [JsonPropertyName("function")]
        public string Function { get; set; } = "None";
    }

    public sealed class ButtonFunctionMappingConfig
    {
        [JsonPropertyName("mappings")]
        public ButtonFunctionMapping[]? Mappings { get; set; } = [];
    }
}
