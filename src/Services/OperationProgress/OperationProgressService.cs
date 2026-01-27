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
                Message = string.Empty,
                Steps = new List<string>(),
                IsCompleted = false,
                Success = true
            };
            _ops[id] = ev;
            OnProgressChanged?.Invoke(ev);
            return id;
        }

        public void ReportProgress(Guid operationId, string message = "")
        {
            if (!_ops.TryGetValue(operationId, out var ev)) return;
            var msg = message ?? string.Empty;
            if (!string.IsNullOrEmpty(msg)) ev.Steps.Add(msg);
            ev.Message = msg;
            OnProgressChanged?.Invoke(ev);
        }

        public void CompleteOperation(Guid operationId, bool success = true, string message = "")
        {
            if (!_ops.TryGetValue(operationId, out var ev)) return;
            if (!string.IsNullOrEmpty(message))
            {
                ev.Steps.Add(message);
                ev.Message = message;
            }
            ev.IsCompleted = true;
            ev.Success = success;
            if (success)
                ReportProgress(operationId, message);
            OnProgressChanged?.Invoke(ev);
            _ops.TryRemove(operationId, out _);
        }
    }
}
