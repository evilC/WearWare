# Buttons

Status: Proposal, not implemented. See also [I2C](i2c.md).

## Goals

- Move the author's four QuickMedia buttons to an Adafruit MCP23017 I2C GPIO expander, while continuing to support direct Pi GPIO buttons.
- Use the same button handling logic for direct and expander GPIO. Support other suitable expanders from `Iot.Device.Bindings` when integrated and tested.
- Configure the hardware in files without requiring code changes. Preserve existing `buttonpins.json` configurations and QuickMedia slot mappings.

## Proposed approach

- The MCP23017 binding is a GPIO driver and can be wrapped in a `GpioController`, allowing reuse of the existing `GpioButton`-based handler. Configure the chip, bus, address, expander pins and required Pi interrupt pins.
- Share the expander and its controller across its buttons. The current handler disposes its controller per button, so ownership and disposal need to change without disrupting other buttons.
- Preserve the current 200 ms debounce and 100 ms suppression of events after each button is created unless testing shows a change is needed. The suppression is not limited to application startup.
- The existing service derives the number of button slots from the configured pin list rather than hard-coding four.

## MCP23017 wiring

- With the existing binding's GPIO callbacks, each used port needs its corresponding interrupt output connected to a free Pi GPIO: INTA for port A and INTB for port B.
- Adafruit advises against using GPA7 and GPB7 as inputs because a pin change during a read can corrupt SDA. Use GPA0-GPA6 and GPB0-GPB6: up to seven buttons with one interrupt pin, or 14 with two.
- More expanders are possible, but with only two free interrupt GPIOs, supporting additional ports would require another approach such as polling or shared-interrupt handling.

## Open questions

- Final configuration format, including whether direct and expander buttons can be mixed.
- Behaviour on missing hardware, bus faults and recovery, especially during an event.
- Confirm interrupt handling, debounce, startup behaviour and disposal on the actual Pi and breakout.

References: [MCP23xxx binding and interrupt support](https://github.com/dotnet/iot/blob/v4.1.0/src/devices/Mcp23xxx/README.md#interrupt-support), [Adafruit MCP23017 pinout](https://learn.adafruit.com/adafruit-mcp23017-i2c-gpio-expander/pinouts).
