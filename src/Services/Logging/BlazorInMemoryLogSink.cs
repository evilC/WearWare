using Serilog.Core;
using Serilog.Events;

// This class seems to be needed to fix the issue with Blazor not recognizing the InMemory sink
namespace WearWare.Services.Logging
{
    public class BlazorInMemoryLogSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();
        private readonly int _maxEvents;

        public BlazorInMemoryLogSink(int maxEvents = 1000)
        {
            _maxEvents = maxEvents;
        }

        public void Emit(LogEvent logEvent)
        {
            _events.Enqueue(logEvent);
            while (_events.Count > _maxEvents && _events.TryDequeue(out _)) { }
        }

        public IReadOnlyList<LogEvent> GetLogEvents(LogEventLevel minLevel, long sinceIndex, out long lastIndex)
        {
            var all = _events.ToList();
            var filtered = all.Where(e => e.Level >= minLevel).Skip((int)sinceIndex).ToList();
            lastIndex = sinceIndex + filtered.Count;
            return filtered;
        }
    }
}
