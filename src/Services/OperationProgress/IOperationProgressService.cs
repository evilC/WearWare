namespace WearWare.Services.OperationProgress
{
    public interface IOperationProgressService
    {
        Guid StartOperation(string title = "");
        void ReportProgress(Guid operationId, int percent, string message = "");
        void CompleteOperation(Guid operationId, bool success = true, string message = "");
        event Action<OperationProgressEvent> OnProgressChanged;
    }
}
