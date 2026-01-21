namespace WearWare.Common
{
    public class ReConvertTaskResult
    {
        public int ExitCode { get; set; }
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int ActualBrightness { get; set; }
    }
}
