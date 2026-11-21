using System.Device.Gpio;

namespace WearWare.Architecture.Button
{
    /// <summary>
    /// Represents a GPIO provider that provides button input functionality.
    /// Could be a GPIO expander running over I2C, but could also be the Pi's native GPIO controller.
    /// </summary>
    public interface IButtonGpioProvider : IDisposable
    {
        /// <summary>
        /// Gets the GpioController for interacting with pins.
        /// </summary>
        GpioController Controller { get; }

        /// <summary>
        /// Gets the number of button pins available from the provider.
        /// </summary>
        int GetButtonPinCount();

        /// <summary>
        /// Gets the pin number of the button at the specified index.
        /// </summary>
        /// <param name="index">The index of the button (0-based).</param>
        /// <returns>The pin number of the button.</returns>
        int GetButtonPinNumber(int index);

        /// <summary>
        /// Gets a human-friendly name identifying the physical pin.
        /// Examples: "GPIO16" for Pi GPIO, "A5" for I2C expander port A pin 5.
        /// </summary>
        /// <param name="pinNumber">The pin number returned by GetButtonPinNumber.</param>
        /// <returns>The human-friendly pin name.</returns>
        string GetPinName(int pinNumber);
    }
}
