using Serilog.Events;
using WearWare.Services.Logging;

namespace WearWare.Components.Pages.Log
{
    public partial class Log
    {
        [Inject] public InMemoryLogService LogService { get; set; } = null!;

        [Inject] public IJSRuntime JSRuntime { get; set; } = null!;

        private List<LogEvent> logEntries = new();
        private long lastIndex = 0;
        private readonly LogEventLevel[] logLevels = (LogEventLevel[])Enum.GetValues(typeof(LogEventLevel));
        private Timer? timer;
        private ElementReference logEntriesDiv;
        private bool shouldScroll = false;
        private bool autoscrollEnabled = true;
        private LogEventLevel _selectedLevel = LogEventLevel.Warning;
        private LogEventLevel selectedLevel
        {
            get => _selectedLevel;
            set
            {
                if (_selectedLevel != value)
                {
                    _selectedLevel = value;
                    // Reload all entries at this level
                    logEntries.Clear();
                    lastIndex = 0;
                    var allEntries = LogService.GetLogEvents(_selectedLevel, 0, out var newLastIndex);
                    logEntries.AddRange(allEntries);
                    lastIndex = newLastIndex;
                    shouldScroll = true;
                    StateHasChanged();
                }
            }
        }

        protected override void OnInitialized()
        {
            timer = new Timer(_ =>
            {
                InvokeAsync(UpdateLog);
            }, null, 0, 1000);
        }

        private void UpdateLog()
        {
            var newEntries = LogService.GetLogEvents(selectedLevel, lastIndex, out var newLastIndex);
            if (newEntries.Count > 0)
            {
                logEntries.AddRange(newEntries);
                lastIndex = newLastIndex;
                shouldScroll = true;
                StateHasChanged();
            }
        }


        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (shouldScroll)
            {
                shouldScroll = false;
                if (autoscrollEnabled)
                {
                    await JSRuntime.InvokeVoidAsync("logAutoscroll", logEntriesDiv);
                }
            }
        }

        private string FormatLogEntry(LogEvent entry)
        {
            return $"[{entry.Timestamp:HH:mm:ss} {entry.Level}] {entry.RenderMessage()}";
        }

        public void Dispose()
        {
            timer?.Dispose();
        }
    }
}