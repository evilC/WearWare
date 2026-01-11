namespace WearWare.Common
{
    public class TaskResult
    {
        public int ExitCode { get; set; }
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
