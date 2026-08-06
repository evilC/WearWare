using RPiRgbLEDMatrix;
using WearWare.Common;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.MatrixConfig;
using System.Threading;

namespace WearWare.Services.MediaController
{
    public class RpiMediaPlayer : IMediaPlayer
    {
        private readonly object _matrixLock = new object();
        private RGBLedMatrix? _matrix;
        private RGBLedCanvas? _canvas;
        private readonly ILogger<RpiMediaPlayer> _logger;
        private readonly string _logTag = "[MEDIAPLAYER]";
        private readonly MatrixConfigService _matrixConfigService;
        private readonly int[] _liveBrightness = [100];
        private volatile int _activeRelativeBrightness = 100;
        private volatile int _isPlaying;

        public RpiMediaPlayer(ILogger<RpiMediaPlayer> logger, MatrixConfigService matrixConfigService)
        {
            _logger = logger;
            _matrixConfigService = matrixConfigService;
            _matrixConfigService.OptionsChanged += MatrixOptionsChanged;
            _matrixConfigService.BrightnessChanged += OnBrightnessChanged;
            MatrixOptionsChanged();
        }

        private void OnBrightnessChanged(int baseBrightness)
        {
            if (Interlocked.CompareExchange(ref _isPlaying, 0, 0) == 0)
            {
                return;
            }

            var combinedBrightness = BrightnessCalculator.CalculateAbsoluteBrightness(baseBrightness, _activeRelativeBrightness);
            _liveBrightness[0] = combinedBrightness;
        }

        /// <summary>
        /// Handles changes to the matrix configuration options.
        /// </summary>
        private void MatrixOptionsChanged()
        {
            lock (_matrixLock)
            {
                try
                {
                    _matrix?.Dispose();
                    var serviceOptions = _matrixConfigService.GetRGBLedMatrixOptions();

                    /*
                    // Enable if ResetFramebufferGlobals is exposed in the RGBLedMatrix library (PR matrix-reset-01)
                    // Reset native framebuffer globals so that InitGPIO() will
                    // reinitialize row/address/pulser state based on new options.
                    try
                    {
                        RGBLedMatrix.ResetFramebufferGlobals();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "{logTag} Exception calling ResetFramebufferGlobals: {Message}", _logTag, ex.Message);
                    }
                    */
                    _matrix = new RGBLedMatrix(serviceOptions);
                    _canvas = _matrix.CreateOffscreenCanvas();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{logTag} Exception applying matrix options: {Message}", _logTag, ex.Message);
                }
            }
        }

        /// <summary>
        /// Plays an fseq file on the RGB LED Matrix.
        /// </summary>
        /// <param name="playableItem"></param> The fseq to play.
        /// <param name="ct"></param> Cancellation token to stop playback
        /// <returns>True if playback completed successfully, false otherwise.</returns>
        public bool PlayFseq(PlayableItem playableItem, CancellationToken ct)
        {
            lock (_matrixLock)
            {
                if (_matrix == null)
                {
                    _logger.LogError("{logTag} Cannot play fseq - Matrix is not initialized.", _logTag);
                    return false;
                }

                var fseqPath = GetFseqPath(playableItem);
                if (!File.Exists(fseqPath))
                {
                    _logger.LogError("{logTag} FSEQ file does not exist: {FseqPath}", _logTag, fseqPath);
                    return false;
                }

                try
                {
                    var options = _matrixConfigService.CloneOptions();
                    var width = (options.Cols ?? 128) * (options.ChainLength ?? 1);
                    var height = (options.Rows ?? 64) * (options.Parallel ?? 1);

                    using var sequence = new FrameSequence(width, height);
                    sequence.ReadFromFile(fseqPath);

                    var baseBrightness = options.Brightness ?? 100;
                    var combinedBrightness = BrightnessCalculator.CalculateAbsoluteBrightness(baseBrightness, playableItem.RelativeBrightness);
                    _activeRelativeBrightness = playableItem.RelativeBrightness;
                    _liveBrightness[0] = combinedBrightness;
                    Interlocked.Exchange(ref _isPlaying, 1);
                    var stop = new[] { 0 };
                    using var stopRegistration = ct.Register(() => stop[0] = 1);

                    switch (playableItem.PlayMode)
                    {
                        case PlayMode.Forever:
                            sequence.PlayForever(_matrix, stop, _liveBrightness);
                            break;
                        case PlayMode.Loop:
                            sequence.PlayCount(_matrix, playableItem.PlayModeValue > 0 ? (uint)playableItem.PlayModeValue : 0u, stop, _liveBrightness);
                            break;
                        case PlayMode.Duration:
                            sequence.PlayDuration(_matrix, playableItem.PlayModeValue > 0 ? (uint)playableItem.PlayModeValue * 1000u : 0u, stop, _liveBrightness);
                            break;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{logTag} Exception in PlayFseq: {Message}", _logTag, ex.Message);
                    return false;
                }
                finally
                {
                    Interlocked.Exchange(ref _isPlaying, 0);
                    try
                    {
                        Clear();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{logTag} Exception clearing screen in PlayFseq finally: {Message}", _logTag, ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Clear the matrix to blank the screen
        /// </summary>
        public void Clear()
        {
            if (_matrix == null || _canvas == null)
            {
                _logger.LogError("{logTag} Cannot Clear - Matrix or canvas is not initialized.", _logTag);
                return;
            }
            _canvas.Clear();
            _matrix.SwapOnVsync(_canvas);
        }

        private string GetFseqPath(PlayableItem playableItem)
        {
            return Path.Combine(PathConfig.Root, playableItem.ParentFolder, $"{playableItem.Name}.fseq");
        }
    }
}