using WearWare.Common.Media;
using WearWare.Services.MediaController;
using WearWare.Architecture.Button;

using Iot.Device.Button;

namespace WearWare.Architecture.QuickMedia
{
    public class QuickMediaHandler : IHandler, IDisposable
    {
        private readonly QuickMediaManager _quickMediaManager;
        private readonly ButtonManager _buttonManager;
        private readonly IMediaPlayer _mediaPlayer;
        private readonly ILogger<QuickMediaHandler> _logger;
        private readonly string _logTag = "[QUICKMEDIAHANDLER]";

        private volatile bool _running = false;
        private Task<bool>? _playTask;
        private CancellationTokenSource? _cts;
        private HandlerState _state = HandlerState.Idle;
        private int? _lastActiveButtonSlot;
        private PlayMode _playingMode;
        private Dictionary<int, int> _buttonIndexToSlot = [];
        private Dictionary<int, GpioButton> _gpioButtons = [];

        public string Name => "QuickMedia";
        public HandlerState State => _state;
        public event Action? StateChanged;
        public event Action<int>? ButtonPressed;
        public event Action<Task<bool>, bool>? PlaybackCompleted;

        public QuickMediaHandler(
            QuickMediaManager quickMediaManager,
            ButtonManager buttonManager,
            IMediaPlayer mediaPlayer,
            ILogger<QuickMediaHandler> logger)
        {
            _quickMediaManager = quickMediaManager;
            _buttonManager = buttonManager;
            _mediaPlayer = mediaPlayer;
            _logger = logger;
            _quickMediaManager.StateChanged += OnQuickMediaManagerStateChanged;
        }

        private void OnQuickMediaManagerStateChanged()
        {
            if (_running && _quickMediaManager.ActiveButtonSlot != _lastActiveButtonSlot)
            {
                _lastActiveButtonSlot = _quickMediaManager.ActiveButtonSlot;
                Stop();
                Start();
            }
        }

        public void Initialize()
        {
            _logger.LogInformation("{tag} starting.", _logTag);

            _quickMediaManager.LoadAllItems();

            if (_quickMediaManager.ActiveButtonItem == null)
            {
                var loadedSlots = _quickMediaManager.GetLoadedButtonSlots();
                if (loadedSlots.Count > 0)
                {
                    _quickMediaManager.SetActiveButton(loadedSlots[0]);
                }
                else
                {
                    _logger.LogWarning("{tag} No quick media items loaded on startup.", _logTag);
                }
            }

            SubscribeToButtonEvents();
            _logger.LogInformation("{tag} QuickMedia items loaded. Not starting playback - waiting for mode switch.", _logTag);
        }

        public void Dispose()
        {
            _logger.LogInformation("{tag} stopping.", _logTag);
            _quickMediaManager.StateChanged -= OnQuickMediaManagerStateChanged;
            try
            {
                Stop();
            }
            finally
            {
                UnsubscribeFromButtonEvents();
            }
        }

        private void SubscribeToButtonEvents()
        {
            if (_buttonManager.Provider == null)
            {
                _logger.LogInformation("{tag} No button provider available, button events disabled.", _logTag);
                return;
            }

            var quickMediaButtons = _buttonManager.GetButtonsForFunction("QuickMedia").ToList();
            if (quickMediaButtons.Count == 0)
            {
                _logger.LogInformation("{tag} No buttons assigned to QuickMedia function.", _logTag);
                return;
            }

            foreach (var button in quickMediaButtons)
            {
                try
                {
                    var buttonNumber = button.Index + 1;
                    var gpioButton = new GpioButton(
                        buttonPin: button.PinNumber,
                        isPullUp: true,
                        hasExternalResistor: false,
                        gpio: _buttonManager.Provider.Controller,
                        shouldDispose: false,
                        debounceTime: TimeSpan.FromMilliseconds(50));
                    
                    gpioButton.Press += (s, e) => OnButtonPressed(buttonNumber);
                    _gpioButtons[button.PinNumber] = gpioButton;
                    
                    _logger.LogInformation("{tag} Subscribed to button {pin} ({name}) -> button {button}.", _logTag, button.PinNumber, button.PinName, buttonNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{tag} Failed to subscribe to button {pin} ({name}).", _logTag, button.PinNumber, button.PinName);
                }
            }
            
            _logger.LogInformation("{tag} Button event handlers initialized and enabled.", _logTag);
        }

        private void UnsubscribeFromButtonEvents()
        {
            foreach (var gpioButton in _gpioButtons.Values)
            {
                try
                {
                    gpioButton?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{tag} Failed to dispose GpioButton.", _logTag);
                }
            }
            _gpioButtons.Clear();
            _buttonIndexToSlot.Clear();
        }

        private void OnButtonPressed(int buttonNumber)
        {
            _logger.LogInformation("{tag} QuickMedia button pressed: button {button}.", _logTag, buttonNumber);

            if (ButtonPressed == null)
                _logger.LogWarning("{tag} No mode handler available for button {button}.", _logTag, buttonNumber);
            else
                ButtonPressed.Invoke(buttonNumber);
        }

        /// <summary>
        /// Starts playback of the active quick media item.
        /// </summary>
        public void Start()
        {
            if (_running)
            {
                _logger.LogWarning("{tag} Start called but playback is already running.", _logTag);
                return;
            }

            if (_playTask != null)
                Stop();

            var item = _quickMediaManager.ActiveButtonItem?.ToPlayableItem();
            if (item == null)
            {
                _logger.LogWarning("{tag} No active quick media item, cannot start playback.", _logTag);
                SetState(HandlerState.Failed);
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _lastActiveButtonSlot = _quickMediaManager.ActiveButtonSlot;
                _playingMode = item.PlayMode;
                _running = true;
                _playTask = Task.Run(() => PlayLoop(item, ct));
                SetState(HandlerState.Running);
                _ = ObservePlaybackAsync(_playTask);
                _logger.LogInformation("{tag} Playback started for slot {slot}.", _logTag, _quickMediaManager.ActiveButtonSlot);
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
            if (_playTask == null)
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

        public bool IsCurrentPlayback(Task<bool> task) => ReferenceEquals(_playTask, task);

        public bool IsPlayingForever(int buttonSlot) =>
            _running && _lastActiveButtonSlot == buttonSlot && _playingMode == PlayMode.Forever;

        private async Task ObservePlaybackAsync(Task<bool> task)
        {
            var success = await task.ConfigureAwait(false);
            PlaybackCompleted?.Invoke(task, success);
        }

        private bool PlayLoop(PlayableItem item, CancellationToken ct)
        {
            try
            {
                var fseqPath = item.GetFseqFilePath();
                if (!File.Exists(fseqPath))
                {
                    _logger.LogWarning("{tag} Quick media fseq file not found: {path}", _logTag, fseqPath);
                    return false;
                }

                var success = _mediaPlayer.PlayFseq(item, ct);
                if (!success && !ct.IsCancellationRequested)
                    _logger.LogError("{tag} Playback failed for quick media item {name}.", _logTag, item.Name);
                return success;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Exception in PlayLoop: {message}", _logTag, ex.Message);
                return false;
            }
            finally
            {
                _running = false;
            }
        }
    }
}






