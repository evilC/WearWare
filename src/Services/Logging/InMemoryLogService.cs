
using Serilog.Events;

namespace WearWare.Services.Logging
{
    public class InMemoryLogService
    {
        private readonly BlazorInMemoryLogSink _sink;

        public InMemoryLogService(BlazorInMemoryLogSink sink)
        {
            _sink = sink;
        }

        public IReadOnlyList<LogEvent> GetLogEvents(LogEventLevel minLevel, long sinceIndex, out long lastIndex)
        {
            return _sink.GetLogEvents(minLevel, sinceIndex, out lastIndex);
        }
    }
}
