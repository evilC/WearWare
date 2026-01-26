using System.Collections.Concurrent;

namespace WearWare.Services.OperationProgress
{
    public class OperationProgressService : IOperationProgressService
    {
        private readonly ConcurrentDictionary<Guid, OperationProgressEvent> _ops = new();

        public event Action<OperationProgressEvent> OnProgressChanged = _ => { };

        public Guid StartOperation(string title = "")
        {
            var id = Guid.NewGuid();
            var ev = new OperationProgressEvent
            {
                OperationId = id,
                Title = string.IsNullOrEmpty(title) ? "Operation" : title,
                Percent = 0,
                Message = string.Empty,
                IsCompleted = false,
                Success = true
            };
            _ops[id] = ev;
            OnProgressChanged?.Invoke(ev);
            return id;
        }

        public void ReportProgress(Guid operationId, int percent, string message = "")
        {
            if (!_ops.TryGetValue(operationId, out var ev)) return;
            ev.Percent = Math.Clamp(percent, 0, 100);
            ev.Message = message ?? string.Empty;
            OnProgressChanged?.Invoke(ev);
        }

        public void CompleteOperation(Guid operationId, bool success = true, string message = "")
        {
            if (!_ops.TryGetValue(operationId, out var ev)) return;
            ev.IsCompleted = true;
            ev.Success = success;
            if (!string.IsNullOrEmpty(message)) ev.Message = message;
            ev.Percent = 100;
            OnProgressChanged?.Invoke(ev);
            _ops.TryRemove(operationId, out _);
        }
    }
}
