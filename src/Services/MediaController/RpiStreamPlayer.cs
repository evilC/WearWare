using RPiRgbLEDMatrix;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.MediaController
{
    public class RpiStreamPlayer : IStreamPlayer
    {
        private readonly object _matrixLock = new object();
        private RGBLedMatrix? _matrix;
        private RGBLedCanvas? _canvas;
        private readonly ILogger<RpiStreamPlayer> _logger;
        private readonly string _logTag = "[STREAMPLAYER]";
        private readonly MatrixConfigService _matrixConfigService;

        public RpiStreamPlayer(ILogger<RpiStreamPlayer> logger, MatrixConfigService matrixConfigService)
        {
            _logger = logger;
            _matrixConfigService = matrixConfigService;
            _matrixConfigService.OptionsChanged += MatrixOptionsChanged;
            MatrixOptionsChanged();
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
        /// Plays a stream file on the RGB LED Matrix.
        /// </summary>
        /// <param name="playableItem"></param> The stream to play.
        /// <param name="ct"></param> Cancellation token to stop playback
        /// <returns>True if playback completed successfully, false otherwise.</returns>
        public bool PlayStream(PlayableItem playableItem, CancellationToken ct)
        {
            lock (_matrixLock)
            {
                if (_matrix == null || _canvas == null)
                {
                    _logger.LogError("{logTag} Cannot play stream - Matrix or canvas is not initialized.", _logTag);
                    return false;
                }
                ContentStreamer? reader = null;
                try
                {
                    var streamPath = GetStreamPath(playableItem);
                    reader = new ContentStreamer(streamPath);
                    if (!reader.IsCompatible(_canvas.Handle))
                    {
                        // Stream is incompatible with the current matrix configuration
                        _logger.LogError("{logTag} Stream {StreamPath} is incompatible with the current matrix configuration.", _logTag, streamPath);
                        return false;
                    }
                    var loopNum = 0u;
                    long endTime = 0;
                    if (playableItem.PlayMode == PlayMode.Duration)
                    {
                        endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + (playableItem.PlayModeValue * 1000);
                    }
                    while (!ct.IsCancellationRequested)
                    {
                        if (playableItem.PlayMode == PlayMode.Duration && DateTimeOffset.Now.ToUnixTimeMilliseconds() >= endTime)
                        {
                            break;
                        }
                        if (!reader.GetNext(_canvas.Handle, out uint delay))
                        {
                            if (ct.IsCancellationRequested)
                            {
                                break;
                            }
                            if (loopNum == uint.MaxValue) break;
                            loopNum++;
                            if (playableItem.PlayMode == PlayMode.Loop && loopNum >= playableItem.PlayModeValue)
                            {
                                break;
                            }
                            reader.Rewind();
                            continue;
                        }
                        _matrix.SwapOnVsync(_canvas);
                        Thread.Sleep((int)(delay / 1000));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{logTag} Exception in PlayStream: {Message}", _logTag, ex.Message);
                }
                finally
                {
                    if (reader != null)
                    {
                        reader.Dispose();
                    }
                    try {
                        // Call clear on the canvas to blank the screen
                        Clear();
                    } catch (Exception ex) {
                        _logger.LogError(ex, "{logTag} Exception clearing screen in PlayStream finally: {Message}", _logTag, ex.Message);
                    }
                }
                return true;
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

        private string GetStreamPath(PlayableItem playableItem)
        {
            return Path.Combine(PathConfig.Root, playableItem.ParentFolder, $"{playableItem.Name}.stream");
        }
    }
}