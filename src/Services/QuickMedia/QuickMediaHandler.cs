using WearWare.Common.Media;
using WearWare.Architecture;
using WearWare.Services.Playback;
using WearWare.Services.Button;
using WearWare.Architecture.Mode;

using Iot.Device.Button;

namespace WearWare.Services.QuickMedia
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
        private Task<bool>? _lastPlaybackTask;
        private Task<bool>? _playbackStoppedForDeletion;
        private CancellationTokenSource? _cts;
        private HandlerState _state = HandlerState.Idle;
        private int? _lastActiveButtonSlot;
        private PlayMode _playingMode;
        private bool _hasControl;
        private Func<DisplayMode, Task<bool>>? _requestControl;
        private Func<DisplayMode, Task>? _releaseControl;
        private Func<bool>? _controlAvailable;
        private Dictionary<int, int> _buttonIndexToSlot = [];
        private Dictionary<int, GpioButton> _gpioButtons = [];

        public string Name => "QuickMedia";
        public HandlerState State => _state;
        public event Action? StateChanged;
        internal SemaphoreSlim TransitionGate => _quickMediaManager.TransitionGate;

        /// <summary>
        /// Connects the QM handler to its manager, buttons, and media player.
        /// </summary>
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
            _quickMediaManager.ItemDeleting += OnItemDeleting;
            _quickMediaManager.ItemDeletionFinished += OnItemDeletionFinished;
        }

        /// <summary>
        /// Stops the item being deleted if it is the current playback.
        /// </summary>
        private void OnItemDeleting(int buttonSlot)
        {
            if (_lastActiveButtonSlot != buttonSlot || _playTask == null)
                return;

            _playbackStoppedForDeletion = _playTask;
            Stop();
        }

        /// <summary>
        /// Restarts the stopped item if its deletion failed and it is still active.
        /// </summary>
        private void OnItemDeletionFinished(int buttonSlot, bool deleted)
        {
            var stoppedTask = _playbackStoppedForDeletion;
            _playbackStoppedForDeletion = null;
            if (!deleted && stoppedTask != null && ReferenceEquals(_lastPlaybackTask, stoppedTask) &&
                _quickMediaManager.ActiveButtonSlot == buttonSlot)
            {
                Start();
            }
        }

        /// <summary>
        /// Restarts playback if the manager changes the active slot while QM is running.
        /// </summary>
        private void OnQuickMediaManagerStateChanged()
        {
            if (_running && _quickMediaManager.ActiveButtonSlot != _lastActiveButtonSlot)
            {
                _lastActiveButtonSlot = _quickMediaManager.ActiveButtonSlot;
                Stop();
                Start();
            }
        }

        /// <summary>
        /// Loads QM items and subscribes to button events without starting playback.
        /// </summary>
        public void Initialize()
        {
            _logger.LogInformation("{tag} starting.", _logTag);

            _quickMediaManager.LoadAllItems();

            SubscribeToButtonEvents();
            _logger.LogInformation("{tag} QuickMedia items loaded. Not starting playback - waiting for mode switch.", _logTag);
        }

        /// <summary>
        /// Stops playback and releases manager and GPIO subscriptions.
        /// </summary>
        public void Dispose()
        {
            _logger.LogInformation("{tag} stopping.", _logTag);
            _hasControl = false;
            _quickMediaManager.StateChanged -= OnQuickMediaManagerStateChanged;
            _quickMediaManager.ItemDeleting -= OnItemDeleting;
            _quickMediaManager.ItemDeletionFinished -= OnItemDeletionFinished;
            try
            {
                Stop();
            }
            finally
            {
                UnsubscribeFromButtonEvents();
            }
        }

        /// <summary>
        /// Subscribes configured QM buttons to their GPIO press events.
        /// </summary>
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

        /// <summary>
        /// Disposes the GPIO buttons and clears their subscriptions.
        /// </summary>
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

        /// <summary>
        /// Dispatches a GPIO press for asynchronous processing.
        /// </summary>
        private void OnButtonPressed(int buttonNumber)
        {
            _ = HandleButtonPressAsync(buttonNumber);
        }

        /// <summary>
        /// Supplies the callbacks used to request and yield playback control.
        /// </summary>
        internal void ConfigureModeControl(Func<DisplayMode, Task<bool>> requestControl, Func<DisplayMode, Task> releaseControl, Func<bool> controlAvailable)
        {
            _requestControl = requestControl;
            _releaseControl = releaseControl;
            _controlAvailable = controlAvailable;
        }

        /// <summary>
        /// Validates a press, requests control when needed, and applies the QM slot change.
        /// </summary>
        private async Task HandleButtonPressAsync(int buttonNumber)
        {
            if (_controlAvailable == null)
            {
                _logger.LogError("{tag} Mode control is not configured.", _logTag);
                return;
            }
            if (!_controlAvailable())
                return;

            await TransitionGate.WaitAsync();
            try
            {
                _logger.LogInformation("{tag} QuickMedia button pressed: button {button}.", _logTag, buttonNumber);
                if (!_quickMediaManager.GetQuickMediaButtonNumbers().Contains(buttonNumber) ||
                    _quickMediaManager.GetButtonItem(buttonNumber) == null)
                {
                    _logger.LogWarning("{tag} QuickMedia button {button} is unassigned or has no item.", _logTag, buttonNumber);
                    return;
                }

                if (_requestControl == null || _releaseControl == null)
                {
                    _logger.LogError("{tag} Mode control is not configured.", _logTag);
                    return;
                }

                if (!_hasControl)
                {
                    if (!await _requestControl(DisplayMode.QuickMedia))
                        return;
                    _hasControl = true;
                }
                if (!ActivateButton(buttonNumber))
                    await YieldControlAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Failed to activate QuickMedia button {button}.", _logTag, buttonNumber);
                await RecoverAndYieldAsync();
            }
            finally
            {
                TransitionGate.Release();
            }
        }

        /// <summary>
        /// Yields playback control to the mode coordinator.
        /// </summary>
        private async Task YieldControlAsync()
        {
            if (_releaseControl == null)
                throw new InvalidOperationException("Mode control is not configured.");

            await _releaseControl(DisplayMode.QuickMedia);
            _hasControl = false;
        }

        /// <summary>
        /// Deactivates QM and attempts to yield control after an error.
        /// </summary>
        private async Task RecoverAndYieldAsync()
        {
            if (!_hasControl)
                return;

            try
            {
                Deactivate();
                await YieldControlAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Failed to yield control after a QuickMedia error.", _logTag);
            }
        }

        /// <summary>
        /// Resumes the saved Forever item when QM is granted control.
        /// </summary>
        internal bool Resume()
        {
            _hasControl = true;
            if (ResumeForever())
                return true;

            _hasControl = false;
            return false;
        }

        /// <summary>
        /// Stops playback without clearing the saved Forever item.
        /// </summary>
        internal void Suspend()
        {
            _hasControl = false;
            Stop();
        }

        /// <summary>
        /// Stops QM and clears both the active and saved Forever slots.
        /// </summary>
        internal void Deactivate()
        {
            Suspend();
            _quickMediaManager.ClearActiveButton();
            _quickMediaManager.ClearForeverButton();
        }

        /// <summary>
        /// Applies a button press to the temporary and Forever items, returning whether QM retains control.
        /// </summary>
        private bool ActivateButton(int buttonSlot)
        {
            var item = _quickMediaManager.GetButtonItem(buttonSlot);
            if (item == null)
            {
                _logger.LogWarning("{tag} No QuickMedia item in slot {slot}.", _logTag, buttonSlot);
                return _running;
            }

            var temporaryPlaying = _running && _playingMode != PlayMode.Forever;
            if (_quickMediaManager.ForeverButtonSlot == buttonSlot)
            {
                _quickMediaManager.ClearForeverButton();
                if (temporaryPlaying)
                    return true;

                Stop();
                _quickMediaManager.ClearActiveButton();
                return false;
            }

            if (item.PlayMode == PlayMode.Forever)
            {
                if (!_quickMediaManager.SetForeverButton(buttonSlot))
                    return _running || ResumeForever();
                if (temporaryPlaying)
                    return true;
            }

            Stop();
            if (_quickMediaManager.SetActiveButton(buttonSlot))
            {
                Start();
                if (State != HandlerState.Failed)
                    return true;
            }

            if (item.PlayMode == PlayMode.Forever)
                _quickMediaManager.ClearForeverButton();
            _quickMediaManager.ClearActiveButton();
            return ResumeForever();
        }

        /// <summary>
        /// Validates and starts the saved Forever item, returning whether playback started.
        /// </summary>
        private bool ResumeForever()
        {
            var buttonSlot = _quickMediaManager.ForeverButtonSlot;
            if (buttonSlot == 0)
                return false;

            if (_quickMediaManager.GetButtonItem(buttonSlot)?.PlayMode != PlayMode.Forever ||
                !_quickMediaManager.SetActiveButton(buttonSlot))
            {
                _logger.LogWarning("{tag} Cannot resume Forever item in slot {slot}.", _logTag, buttonSlot);
                _quickMediaManager.ClearForeverButton();
                _quickMediaManager.ClearActiveButton();
                return false;
            }

            Start();
            if (State != HandlerState.Failed)
                return true;

            _quickMediaManager.ClearForeverButton();
            _quickMediaManager.ClearActiveButton();
            return false;
        }

        /// <summary>
        /// Cleans up the completed item and resumes Forever if available.
        /// </summary>
        private bool CompletePlayback(bool success)
        {
            var finishedForever = _playingMode == PlayMode.Forever &&
                                  _lastActiveButtonSlot == _quickMediaManager.ForeverButtonSlot;
            Stop();
            _quickMediaManager.ClearActiveButton();
            if (finishedForever)
                _quickMediaManager.ClearForeverButton();
            if (!success)
                _logger.LogError("{tag} QuickMedia playback failed.", _logTag);
            return ResumeForever();
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
                _lastPlaybackTask = _playTask;
                SetState(HandlerState.Running);
                _ = ObservePlaybackAsync(_playTask, ct);
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

        /// <summary>
        /// Updates the handler state and notifies subscribers when it changes.
        /// </summary>
        private void SetState(HandlerState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                StateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Returns whether the specified button's Forever item is currently playing.
        /// </summary>
        public bool IsPlayingForever(int buttonSlot) =>
            _running && _lastActiveButtonSlot == buttonSlot && _playingMode == PlayMode.Forever;

        /// <summary>
        /// Processes completion of the current playback and yields control if no QM item remains.
        /// </summary>
        private async Task ObservePlaybackAsync(Task<bool> task, CancellationToken ct)
        {
            var success = await task.ConfigureAwait(false);
            await TransitionGate.WaitAsync();
            try
            {
                if (!_hasControl || !ReferenceEquals(_lastPlaybackTask, task))
                    return;

                if (!CompletePlayback(success || ct.IsCancellationRequested))
                    await YieldControlAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Failed to complete QuickMedia playback.", _logTag);
                await RecoverAndYieldAsync();
            }
            finally
            {
                TransitionGate.Release();
            }
        }

        /// <summary>
        /// Plays the item's frame sequence and reports whether playback succeeded.
        /// </summary>
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






