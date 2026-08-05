using RPiRgbLEDMatrix;
using WearWare.Common;
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

                    if (!TryGetFseqSingleLoopDurationUs(fseqPath, out var singleLoopDurationUs))
                    {
                        _logger.LogError("{logTag} Could not determine loop duration for fseq {FseqPath}", _logTag, fseqPath);
                        return false;
                    }

                    var baseBrightness = options.Brightness ?? 100;
                    var combinedBrightness = BrightnessCalculator.CalculateAbsoluteBrightness(baseBrightness, playableItem.RelativeBrightness);
                    var brightness = new[] { Math.Clamp(combinedBrightness, 1, 100) };
                    var stop = new[] { 0 };

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    switch (playableItem.PlayMode)
                    {
                        case PlayMode.Duration:
                            linkedCts.CancelAfter(TimeSpan.FromSeconds(playableItem.PlayModeValue));
                            break;
                        case PlayMode.Loop:
                            // FrameSequence.Play loops forever; for loop mode we stop after N full sequence durations.
                            linkedCts.CancelAfter(TimeSpan.FromMilliseconds((singleLoopDurationUs * playableItem.PlayModeValue) / 1000.0));
                            break;
                    }

                    var playTask = Task.Run(() => sequence.Play(_matrix, stop, brightness));

                    while (!linkedCts.Token.IsCancellationRequested)
                    {
                        Thread.Sleep(20);
                    }

                    stop[0] = 1;
                    try
                    {
                        playTask.Wait();
                    }
                    catch (AggregateException ex)
                    {
                        foreach (var inner in ex.InnerExceptions)
                        {
                            _logger.LogError(inner, "{logTag} Exception in PlayFseq task: {Message}", _logTag, inner.Message);
                        }
                        return false;
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

        private string GetStreamPath(PlayableItem playableItem)
        {
            return Path.Combine(PathConfig.Root, playableItem.ParentFolder, $"{playableItem.Name}.stream");
        }

        private string GetFseqPath(PlayableItem playableItem)
        {
            return Path.Combine(PathConfig.Root, playableItem.ParentFolder, $"{playableItem.Name}.fseq");
        }

        private bool TryGetFseqSingleLoopDurationUs(string fseqPath, out long durationUs)
        {
            durationUs = 0;
            const uint expectedMagic = 0x51455346;

            try
            {
                using var fs = File.OpenRead(fseqPath);
                using var br = new BinaryReader(fs);
                if (fs.Length < 24)
                {
                    return false;
                }

                var magic = br.ReadUInt32();
                var version = br.ReadUInt32();
                _ = br.ReadUInt32(); // width
                _ = br.ReadUInt32(); // height
                var frameCount = br.ReadUInt32();
                var frameSize = br.ReadUInt32();

                if (magic != expectedMagic || version != 1 || frameCount == 0 || frameSize == 0)
                {
                    return false;
                }

                for (uint i = 0; i < frameCount; i++)
                {
                    if (fs.Position + 4 > fs.Length)
                    {
                        return false;
                    }

                    var holdUs = br.ReadUInt32();
                    if (durationUs > long.MaxValue - holdUs)
                    {
                        return false;
                    }
                    durationUs += holdUs;

                    if (fs.Position + frameSize > fs.Length)
                    {
                        return false;
                    }
                    fs.Seek(frameSize, SeekOrigin.Current);
                }

                return durationUs > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}