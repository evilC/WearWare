using System.Collections.Concurrent;

namespace WearWare.Services.OperationProgress
{
    public class OperationProgressService : IOperationProgressService
    {
        private readonly ConcurrentDictionary<Guid, OperationProgressEvent> _ops = new();
        // Simple sequential lock: only one operation active at a time
        private readonly System.Threading.SemaphoreSlim _operationLock = new(1, 1);
        private Guid _lockOwner = Guid.Empty;

        public event Action<OperationProgressEvent> OnProgressChanged = _ => { };
        public async Task<Guid> StartOperation(string title = "")
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
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
            // mark lock owner
            _lockOwner = id;
            // Publish a snapshot so subscribers get an immutable copy (helps Blazor diffs)
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
            OnProgressChanged?.Invoke(ev);
            // remove stored op
            _ops.TryRemove(operationId, out _);
            // if this operation held the sequential lock, release it so next can start
            if (_lockOwner == operationId)
            {
                _lockOwner = Guid.Empty;
                try { _operationLock.Release(); } catch { }
            }
        }
    }
}
