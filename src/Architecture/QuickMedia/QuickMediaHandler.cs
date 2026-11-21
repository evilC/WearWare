using WearWare.Common.Media;
using WearWare.Services.MediaController;

namespace WearWare.Architecture.QuickMedia
{
    public class QuickMediaHandler : IHandler, IHostedService
    {
        private readonly QuickMediaManager _quickMediaManager;
        private readonly IMediaPlayer _mediaPlayer;
        private readonly ILogger<QuickMediaHandler> _logger;
        private readonly string _logTag = "[QUICKMEDIAHANDLER]";

        private volatile bool _running = false;
        private Task? _playTask;
        private CancellationTokenSource? _cts;
        private HandlerState _state = HandlerState.Idle;
        private int? _lastActiveButtonNumber;

        public string Name => "QuickMedia";
        public HandlerState State => _state;
        public event Action? StateChanged;

        public QuickMediaHandler(
            QuickMediaManager quickMediaManager,
            IMediaPlayer mediaPlayer,
            ILogger<QuickMediaHandler> logger)
        {
            _quickMediaManager = quickMediaManager;
            _mediaPlayer = mediaPlayer;
            _logger = logger;
            _quickMediaManager.StateChanged += OnQuickMediaManagerStateChanged;
        }

        private void OnQuickMediaManagerStateChanged()
        {
            if (_running && _quickMediaManager.ActiveButtonNumber != _lastActiveButtonNumber)
            {
                _lastActiveButtonNumber = _quickMediaManager.ActiveButtonNumber;
                Stop();
                Start();
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} starting.", _logTag);

            _quickMediaManager.LoadAllItems();

            if (_quickMediaManager.ActiveButtonItem == null)
            {
                var loadedButtons = _quickMediaManager.GetLoadedButtonNumbers();
                if (loadedButtons.Count > 0)
                {
                    _quickMediaManager.SetActiveButton(loadedButtons[0]);
                }
                else
                {
                    _logger.LogWarning("{tag} No quick media items loaded on startup.", _logTag);
                }
            }

            _logger.LogInformation("{tag} QuickMedia items loaded. Not starting playback - waiting for mode switch.", _logTag);
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} stopping.", _logTag);
            Stop();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Starts playback of the active quick media item (looping forever).
        /// </summary>
        public void Start()
        {
            if (_running)
            {
                _logger.LogWarning("{tag} Start called but playback is already running.", _logTag);
                return;
            }

            if (_quickMediaManager.ActiveButtonItem == null)
            {
                _logger.LogWarning("{tag} No active quick media item, cannot start playback.", _logTag);
                SetState(HandlerState.Failed);
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _running = true;
                _lastActiveButtonNumber = _quickMediaManager.ActiveButtonNumber;
                _playTask = Task.Run(() => PlayLoop(ct));
                SetState(HandlerState.Running);
                _logger.LogInformation("{tag} Playback started for button {button}.", _logTag, _quickMediaManager.ActiveButtonNumber);
            }
            catch (Exception ex)
            {
                _running = false;
                _cts?.Dispose();
                _cts = null;
                SetState(HandlerState.Failed);
                _logger.LogError(ex, "{tag} Exception starting PlayLoop task: {message}", _logTag, ex.Message);
            }
        }

        /// <summary>
        /// Stops playback.
        /// </summary>
        public void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _cts?.Cancel();

            try
            {
                _playTask?.Wait();
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                {
                    _logger.LogError(inner, "{tag} Exception during Stop: {message}", _logTag, inner.Message);
                }
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _playTask = null;
                _mediaPlayer.Clear();
            }

            SetState(HandlerState.Idle);
            _logger.LogInformation("{tag} Playback stopped.", _logTag);
        }

        private void SetState(HandlerState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                StateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Main playback loop - plays the item according to its PlayMode.
        /// </summary>
        private void PlayLoop(CancellationToken ct)
        {
            try
            {
                while (_running && !ct.IsCancellationRequested)
                {
                    var item = _quickMediaManager.ActiveButtonItem;
                    if (item == null)
                    {
                        _logger.LogWarning("{tag} Active item became null during playback.", _logTag);
                        break;
                    }

                    if (!item.Enabled)
                    {
                        _logger.LogWarning("{tag} Quick media item is disabled, cannot play.", _logTag);
                        break;
                    }

                    var fseqPath = item.GetFseqFilePath();
                    if (!File.Exists(fseqPath))
                    {
                        _logger.LogWarning("{tag} Quick media fseq file not found: {path}", _logTag, fseqPath);
                        break;
                    }

                    _mediaPlayer.PlayFseq(item, ct);

                    if (ct.IsCancellationRequested)
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Exception in PlayLoop: {message}", _logTag, ex.Message);
                SetState(HandlerState.Failed);
            }
            finally
            {
                _running = false;
            }
        }
    }
}
