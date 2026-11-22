# Buttons

Status: Configurable Pi GPIO and MCP23017 providers and central button routing are implemented. Hardware testing and recovery behavior remain future work. See also [I2C](i2c.md).

## Goals

- Support either configured Raspberry Pi GPIO pins or an Adafruit MCP23017 I2C GPIO expander for buttons. Only one provider is active at a time.
- Share the existing `GpioButton` handling logic between providers.
- Configure the provider and button pins in `config/buttonsconfig.json`. Its empty `PiGpio` configuration is a safe default.

## Current implementation

- `PiGpio` uses an explicit ordered list of available BCM GPIO pins, accounting for the installed matrix HAT and other peripherals. `excludeI2cPins` rejects GPIO2/3 in that list when true; it never remaps the configured pins.
- `Mcp23017` uses an ordered list of driver pin numbers plus two interrupt pin entries in IA, IB order. The I2C bus and address are fixed to bus 1 and `0x20`.
- Both providers expose a shared `GpioController`. The central button handler subscribes to all configured pins and routes presses according to their current function.
- Each provider owns its controller resources; the MCP23017 provider also owns the Pi GPIO controller used for interrupts and the I2C device. Individual buttons do not dispose shared controllers.
- If `buttonsconfig.json` is missing, WearWare creates an empty `PiGpio` configuration. Invalid config is logged and leaves buttons disabled without stopping playlist playback. The former `buttonpins.json` is no longer read.
- The Buttons page assigns `None`, `QuickMedia`, `Jog Next` or `Jog Previous` to each pin and saves the mapping in `config/buttonfunctionmapping.json`. Changes take effect immediately. QuickMedia button numbers correspond to the configured pin order, including pins assigned to other functions.
- Jog presses are routed to the active mode. Physics uses them to select the next or previous simulation, wrapping at either end and saving the selection. Jog does nothing in Playlist or while QuickMedia is active.
- Reassigning a QuickMedia button stops its active item or clears its saved Forever item. Other QuickMedia playback continues or resumes as appropriate; otherwise control returns to the selected normal mode. The slot's saved item and files are deleted; library originals are preserved.
- `GpioButton` uses its default pull-up and resistor settings and its default debounce time of zero. Physical debounce behaviour still needs testing.

## MCP23017 wiring

- With the existing binding's GPIO callbacks, each used port needs its corresponding interrupt output connected to a free Pi GPIO: INTA for port A and INTB for port B.
- Adafruit advises against using GPA7 and GPB7 as inputs because a pin change during a read can corrupt SDA. Use GPA0-GPA6 and GPB0-GPB6: up to seven buttons with one interrupt pin, or 14 with two.
- More expanders are possible, but with only two free interrupt GPIOs, supporting additional ports would require another approach such as polling or shared-interrupt handling.

## Remaining work

- Behaviour on missing hardware, bus faults and recovery, especially during an event.
- Confirm interrupt handling, debounce, startup behaviour and disposal on the actual Pi and breakout.

References: [MCP23xxx binding and interrupt support](https://github.com/dotnet/iot/blob/v4.2.0/src/devices/Mcp23xxx/README.md#interrupt-support), [Adafruit MCP23017 pinout](https://learn.adafruit.com/adafruit-mcp23017-i2c-gpio-expander/pinouts).
