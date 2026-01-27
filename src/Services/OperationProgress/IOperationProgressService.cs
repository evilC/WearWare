namespace WearWare.Services.OperationProgress
{
    public interface IOperationProgressService
    {
        Task<Guid> StartOperation(string title = "");
        void ReportProgress(Guid operationId, string message = "");
        void CompleteOperation(Guid operationId, bool success = true, string message = "");
        event Action<OperationProgressEvent> OnProgressChanged;
    }
}
