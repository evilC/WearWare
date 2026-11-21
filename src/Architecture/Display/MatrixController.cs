namespace WearWare.Architecture.Display
{
    /// <summary>
    /// Implements exclusive matrix display control with queued handoff between handlers.
    /// Only one handler can have control at a time. When a new handler requests control,
    /// the current holder is notified to release gracefully before the new handler gains access.
    /// </summary>
    public sealed class MatrixController : IMatrixController
    {
        private readonly SemaphoreSlim _control = new SemaphoreSlim(1, 1);
        private readonly IMatrixDisplay _display;
        private readonly ILogger<MatrixController> _logger;
        private IHandler? _currentController;
        private volatile bool _disposed;

        public IHandler? CurrentController => Volatile.Read(ref _currentController);
        public event Action? ControllerChanged;

        public MatrixController(IMatrixDisplay display, ILogger<MatrixController> logger)
        {
            ArgumentNullException.ThrowIfNull(display);
            ArgumentNullException.ThrowIfNull(logger);
            _display = display;
            _logger = logger;
        }

        public async Task<IMatrixDisplay> RequestControlAsync(IHandler requestingHandler, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(requestingHandler);

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MatrixController));
            }

            var currentHolder = Volatile.Read(ref _currentController);
            if (currentHolder == requestingHandler)
            {
                _logger.LogWarning("[MATRIX-CTRL] Handler '{HandlerName}' requested control it already holds.", requestingHandler.Name);
                return _display;
            }

            _logger.LogDebug("[MATRIX-CTRL] Handler '{HandlerName}' requesting control (current holder: '{CurrentHolder}').",
                requestingHandler.Name, currentHolder?.Name ?? "none");

            try
            {
                await _control.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("[MATRIX-CTRL] Control request from '{HandlerName}' was cancelled.", requestingHandler.Name);
                throw;
            }

            try
            {
                currentHolder = Volatile.Read(ref _currentController);
                if (currentHolder == requestingHandler)
                {
                    _logger.LogDebug("[MATRIX-CTRL] Handler '{HandlerName}' already holds control.", requestingHandler.Name);
                    return _display;
                }

                Volatile.Write(ref _currentController, requestingHandler);
                _logger.LogDebug("[MATRIX-CTRL] Handler '{HandlerName}' gained matrix control.", requestingHandler.Name);

                RaiseControllerChanged();
                return _display;
            }
            finally
            {
                if (Volatile.Read(ref _currentController) != requestingHandler)
                {
                    _control.Release();
                }
            }
        }

        public void ReleaseControl(IHandler releasingHandler)
        {
            ArgumentNullException.ThrowIfNull(releasingHandler);

            var currentHolder = Volatile.Read(ref _currentController);
            if (currentHolder != releasingHandler)
            {
                _logger.LogWarning(
                    "[MATRIX-CTRL] Handler '{ReleasingHandler}' attempted to release control, but '{CurrentHolder}' holds it.",
                    releasingHandler.Name, currentHolder?.Name ?? "none");
                throw new InvalidOperationException(
                    $"Handler '{releasingHandler.Name}' does not currently hold matrix control.");
            }

            Volatile.Write(ref _currentController, null);
            _control.Release();
            _logger.LogDebug("[MATRIX-CTRL] Handler '{HandlerName}' released matrix control.", releasingHandler.Name);
            RaiseControllerChanged();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _control.Dispose();
            var holder = Volatile.Read(ref _currentController);
            if (holder != null)
            {
                Volatile.Write(ref _currentController, null);
                RaiseControllerChanged();
            }
        }

        private void RaiseControllerChanged()
        {
            var handlers = ControllerChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MATRIX-CTRL] A controller-changed listener failed.");
                }
            }
        }
    }
}
