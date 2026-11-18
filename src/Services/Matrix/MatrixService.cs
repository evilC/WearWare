using RPiRgbLEDMatrix;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.Matrix
{
    public sealed class MatrixService : IDisposable
    {
        private readonly object _matrixLock = new object();
        private readonly ILogger<MatrixService> _logger;
        private readonly MatrixConfigService? _config;
        private RGBLedMatrix? _matrix;
        private RGBLedCanvas? _canvas;
        private bool _disposed;

        public MatrixService(ILogger<MatrixService> logger, MatrixConfigService config)
        {
            _logger = logger;
            _config = config;
            _config.OptionsChanged += MatrixOptionsChanged;
            MatrixOptionsChanged();
        }

        internal MatrixService(ILogger<MatrixService> logger, LedMatrixOptionsConfig options)
        {
            _logger = logger;
            ReplaceMatrix(options);
        }

        private void MatrixOptionsChanged()
        {
            lock (_matrixLock)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    ReplaceMatrix(_config!.CloneOptions());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MATRIX] Exception applying matrix options: {Message}", ex.Message);
                }
            }
        }

        private void ReplaceMatrix(LedMatrixOptionsConfig options)
        {
            var oldMatrix = _matrix;
            _matrix = null;
            _canvas = null;
            oldMatrix?.Dispose();

            var matrix = new RGBLedMatrix(options.ToRGBLedMatrixOptions());
            try
            {
                var canvas = matrix.CreateOffscreenCanvas();
                _matrix = matrix;
                _canvas = canvas;
            }
            catch
            {
                matrix.Dispose();
                throw;
            }
        }

        public (int Width, int Height) GetDimensions()
        {
            lock (_matrixLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                var canvas = _canvas ?? throw new InvalidOperationException("Matrix is not initialized.");
                return (canvas.Width, canvas.Height);
            }
        }

        // The callback must complete synchronously and must not retain either reference.
        public T WithMatrix<T>(Func<RGBLedMatrix, RGBLedCanvas, T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            lock (_matrixLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_matrix == null || _canvas == null)
                {
                    throw new InvalidOperationException("Matrix is not initialized.");
                }

                return action(_matrix, _canvas);
            }
        }

        public void WithMatrix(Action<RGBLedMatrix, RGBLedCanvas> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            WithMatrix((matrix, canvas) =>
            {
                action(matrix, canvas);
                return true;
            });
        }

        public bool Clear()
        {
            lock (_matrixLock)
            {
                if (_matrix == null || _canvas == null)
                {
                    return false;
                }

                _canvas.Clear();
                _matrix.SwapOnVsync(_canvas);
                return true;
            }
        }

        public void Dispose()
        {
            if (_config != null)
            {
                _config.OptionsChanged -= MatrixOptionsChanged;
            }

            lock (_matrixLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                var matrix = _matrix;
                _matrix = null;
                _canvas = null;
                matrix?.Dispose();
            }
        }
    }
}
