using System.Collections.Concurrent;
using System.Threading.Channels;

namespace WearWare.Services.OperationProgress
{
    public class OperationProgressService : IOperationProgressService, IDisposable
    {
        private readonly ConcurrentDictionary<Guid, OperationProgressEvent> _ops = new();
        // Simple sequential lock: only one operation active at a time
        private readonly SemaphoreSlim _operationLock = new(1, 1);
        private Guid _lockOwner = Guid.Empty;
        private readonly Channel<OperationProgressEvent> _channel;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _consumerTask;

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
            EnqueueSnapshot(ev);
            return id;
        }

        public void ReportProgress(Guid operationId, string message = "")
        {
            if (!_ops.TryGetValue(operationId, out var ev)) return;
            var msg = message ?? string.Empty;
            if (!string.IsNullOrEmpty(msg)) ev.Steps.Add(msg);
            ev.Message = msg;
            EnqueueSnapshot(ev);
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
            EnqueueSnapshot(ev);
            // remove stored op
            _ops.TryRemove(operationId, out _);
            // if this operation held the sequential lock, release it so next can start
            if (_lockOwner == operationId)
            {
                _lockOwner = Guid.Empty;
                try { _operationLock.Release(); } catch { }
            }
        }

        public OperationProgressService()
        {
            _channel = Channel.CreateUnbounded<OperationProgressEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            _consumerTask = Task.Run(() => ConsumeLoopAsync(_cts.Token));
        }

        private void EnqueueSnapshot(OperationProgressEvent ev)
        {
            var snapshot = new OperationProgressEvent
            {
                OperationId = ev.OperationId,
                Title = ev.Title,
                Message = ev.Message,
                Steps = ev.Steps?.ToList() ?? new List<string>(),
                IsCompleted = ev.IsCompleted,
                Success = ev.Success
            };
            try
            {
                // unbounded channel should almost always accept TryWrite
                if (!_channel.Writer.TryWrite(snapshot))
                {
                    // fallback: queue asynchronously (fire-and-forget)
                    _ = _channel.Writer.WriteAsync(snapshot, _cts.Token).AsTask();
                }
            }
            catch { }
        }

        private async Task ConsumeLoopAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var ev in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        OnProgressChanged?.Invoke(ev);
                    }
                    catch { }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _channel.Writer.TryComplete(); } catch { }
            try { _consumerTask?.Wait(2000); } catch { }
            _cts.Dispose();
        }
    }
}
