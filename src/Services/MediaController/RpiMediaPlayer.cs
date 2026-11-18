using RPiRgbLEDMatrix;
using WearWare.Common;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.Matrix;
using WearWare.Services.MatrixConfig;
using System.Threading;

namespace WearWare.Services.MediaController
{
    public class RpiMediaPlayer : IMediaPlayer
    {
        private readonly MatrixService _matrixService;
        private readonly ILogger<RpiMediaPlayer> _logger;
        private readonly string _logTag = "[MEDIAPLAYER]";
        private readonly MatrixConfigService _matrixConfigService;
        private readonly int[] _liveBrightness = [100];
        private volatile int _currentBrightness = 100;
        private volatile int _activeRelativeBrightness = 100;
        private volatile int _isPlaying;

        public RpiMediaPlayer(ILogger<RpiMediaPlayer> logger, MatrixConfigService matrixConfigService, MatrixService matrixService)
        {
            _logger = logger;
            _matrixConfigService = matrixConfigService;
            _matrixService = matrixService;
            _matrixConfigService.BrightnessChanged += OnBrightnessChanged;
            _currentBrightness = _matrixConfigService.GetCurrentBrightness();
        }

        private void OnBrightnessChanged(int currentBrightness)
        {
            _currentBrightness = currentBrightness;

            if (Interlocked.CompareExchange(ref _isPlaying, 0, 0) == 0)
            {
                return;
            }

            var combinedBrightness = BrightnessCalculator.CalculateAbsoluteBrightness(currentBrightness, _activeRelativeBrightness);
            _liveBrightness[0] = combinedBrightness;
        }

        /// <summary>
        /// Plays an fseq file on the RGB LED Matrix.
        /// </summary>
        /// <param name="playableItem"></param> The fseq to play.
        /// <param name="ct"></param> Cancellation token to stop playback
        /// <returns>True if playback completed successfully, false otherwise.</returns>
        public bool PlayFseq(PlayableItem playableItem, CancellationToken ct)
        {
            try
            {
                return _matrixService.WithMatrix((matrix, canvas) =>
                {
                    var fseqPath = GetFseqPath(playableItem);
                    if (!File.Exists(fseqPath))
                    {
                        _logger.LogError("{logTag} FSEQ file does not exist: {FseqPath}", _logTag, fseqPath);
                        return false;
                    }

                    try
                    {
                        using var sequence = new FrameSequence(canvas.Width, canvas.Height);
                        sequence.ReadFromFile(fseqPath);

                        var combinedBrightness = BrightnessCalculator.CalculateAbsoluteBrightness(_currentBrightness, playableItem.RelativeBrightness);
                        _activeRelativeBrightness = playableItem.RelativeBrightness;
                        _liveBrightness[0] = combinedBrightness;
                        Interlocked.Exchange(ref _isPlaying, 1);
                        var stop = new[] { 0 };
                        using var stopRegistration = ct.Register(() => stop[0] = 1);

                        switch (playableItem.PlayMode)
                        {
                            case PlayMode.Forever:
                                sequence.PlayForever(matrix, stop, _liveBrightness);
                                break;
                            case PlayMode.Loop:
                                sequence.PlayCount(matrix, playableItem.PlayModeValue > 0 ? (uint)playableItem.PlayModeValue : 0u, stop, _liveBrightness);
                                break;
                            case PlayMode.Duration:
                                sequence.PlayDuration(matrix, playableItem.PlayModeValue > 0 ? (uint)playableItem.PlayModeValue * 1000u : 0u, stop, _liveBrightness);
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
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{logTag} Cannot play fseq: {Message}", _logTag, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Clear the matrix to blank the screen
        /// </summary>
        public void Clear()
        {
            if (!_matrixService.Clear())
            {
                _logger.LogError("{logTag} Cannot Clear - Matrix or canvas is not initialized.", _logTag);
            }
        }

        private string GetFseqPath(PlayableItem playableItem)
        {
            return Path.Combine(PathConfig.Root, playableItem.ParentFolder, $"{playableItem.Name}.fseq");
        }
    }
}
