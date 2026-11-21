using WearWare.Common.Media;
using WearWare.Services.MediaController;
using WearWare.Architecture.Button;
using System.Device.Gpio;

namespace WearWare.Architecture.QuickMedia
{
    public class QuickMediaHandler : IHandler, IHostedService
    {
        private readonly QuickMediaManager _quickMediaManager;
        private readonly ButtonManager _buttonManager;
        private readonly IMediaPlayer _mediaPlayer;
        private readonly ILogger<QuickMediaHandler> _logger;
        private readonly string _logTag = "[QUICKMEDIAHANDLER]";

        private volatile bool _running = false;
        private Task? _playTask;
        private CancellationTokenSource? _cts;
        private HandlerState _state = HandlerState.Idle;
        private int? _lastActiveButtonSlot;
        private Dictionary<int, int> _buttonIndexToSlot = [];

        public string Name => "QuickMedia";
        public HandlerState State => _state;
        public event Action? StateChanged;

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

        public async Task StartAsync(CancellationToken cancellationToken)
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
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} stopping.", _logTag);
            Stop();
            UnsubscribeFromButtonEvents();
            await Task.CompletedTask;
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

            _buttonIndexToSlot.Clear();
            int slot = 1;
            foreach (var button in quickMediaButtons)
            {
                _buttonIndexToSlot[button.PinNumber] = slot;
                try
                {
                    _buttonManager.Provider.Controller.OpenPin(button.PinNumber, PinMode.Input);
                    _buttonManager.Provider.Controller.RegisterCallbackForPinValueChangedEvent(
                        button.PinNumber,
                        PinEventTypes.Falling,
                        (object s, PinValueChangedEventArgs e) => OnButtonPressed(button.PinNumber));
                    _logger.LogInformation("{tag} Subscribed to button {pin} ({name}) -> slot {slot}.", _logTag, button.PinNumber, button.PinName, slot);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{tag} Failed to subscribe to button {pin} ({name}).", _logTag, button.PinNumber, button.PinName);
                }
                slot++;
            }
        }

        private void UnsubscribeFromButtonEvents()
        {
            if (_buttonManager.Provider == null)
                return;

            var handlers = new Dictionary<int, PinChangeEventHandler>();
            foreach (var pinNumber in _buttonIndexToSlot.Keys)
            {
                try
                {
                    if (_buttonManager.Provider.Controller.IsPinOpen(pinNumber))
                    {
                        _buttonManager.Provider.Controller.ClosePin(pinNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{tag} Failed to unsubscribe from button {pin}.", _logTag, pinNumber);
                }
            }
            _buttonIndexToSlot.Clear();
        }

        private void OnButtonPressed(int pinNumber)
        {
            if (!_buttonIndexToSlot.TryGetValue(pinNumber, out int slot))
            {
                _logger.LogWarning("{tag} Button pressed on unknown pin {pin}.", _logTag, pinNumber);
                return;
            }

            _logger.LogInformation("{tag} QuickMedia button pressed: slot {slot}.", _logTag, slot);

            if (_quickMediaManager.SetActiveButton(slot))
            {
                Stop();
                Start();
            }
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
                _lastActiveButtonSlot = _quickMediaManager.ActiveButtonSlot;
                _playTask = Task.Run(() => PlayLoop(ct));
                SetState(HandlerState.Running);
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

                    var fseqPath = item.GetFseqFilePath();
                    if (!File.Exists(fseqPath))
                    {
                        _logger.LogWarning("{tag} Quick media fseq file not found: {path}", _logTag, fseqPath);
                        break;
                    }

                    var playableItem = item.ToPlayableItem();
                    _mediaPlayer.PlayFseq(playableItem, ct);

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
