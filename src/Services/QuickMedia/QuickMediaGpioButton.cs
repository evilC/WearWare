using System.Device.Gpio;
using Iot.Device.Button;
using WearWare.Common.Media;
using WearWare.Services.MediaController;
using System.Threading;

namespace WearWare.Services.QuickMedia
{
    public class QuickMediaGpioButton : IQuickMediaButton
    {
        private MediaControllerService _mediaController;
        private GpioController _controller;
        private GpioButton _button;
        public int ButtonNumber { get; }
        public PlayableItem Item { get; }
        // Initialized flag
        // This is IMPORTANT!
        // It seems that buttons can be floating on startup, and will trigger on the first run after boot.
        // So we instantiate the buttons, and then afterwards initialize them.
        // In the ButtonDown event, we ignore any presses until initialized is true.
        private bool _initialized = false;

        public QuickMediaGpioButton(MediaControllerService mediaController, GpioController controller, int buttonNumber, PlayableItem item)
        {
            _mediaController = mediaController;
            _controller = controller;
            ButtonNumber = buttonNumber;
            Item = item;

            if (Config.ButtonPins.PinNumbers.Count <= buttonNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(buttonNumber), "Button number exceeds configured pins.");
            }
            var pinNumber = Config.ButtonPins.PinNumbers[buttonNumber];
            _button = new GpioButton(buttonPin: pinNumber, isPullUp: true, hasExternalResistor: false, gpio: _controller, shouldDispose: true, debounceTime: new TimeSpan(0, 0, 0, 0, 200));
            _button.ButtonDown += (sender, e) =>
            {
                if (!_initialized) return;
                Console.WriteLine( $"({DateTime.Now}) Button {ButtonNumber} down");
                _mediaController.PlayQuickMedia(Item);
            };

            // _button.ButtonUp += (sender, e) =>
            // {
            //     Console.WriteLine( $"({DateTime.Now}) Button {ButtonNumber} up");
            // };

            // Delay initialization of the button to avoid false triggers on startup
            var t = new Thread(() =>
            {
                Thread.Sleep(100);
                _initialized = true;
            }) { IsBackground = true };
            t.Start();
        }

        public void Dispose()
        {
            _button.Dispose();
        }
    }
}