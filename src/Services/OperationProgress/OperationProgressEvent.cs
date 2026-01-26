namespace WearWare.Services.OperationProgress
{
    public class OperationProgressEvent
    {
        public Guid OperationId { get; set; }
        public string Title { get; set; } = string.Empty;
        // Percent removed — use message/state only
        public string Message { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public bool Success { get; set; }
    }
}
