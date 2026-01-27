namespace WearWare.Services.OperationProgress
{
    public class OperationProgressEvent
    {
        public Guid OperationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string> Steps { get; set; } = new List<string>();
        public bool IsCompleted { get; set; }
        public bool Success { get; set; }
    }
}
