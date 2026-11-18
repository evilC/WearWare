# I2C

Status: Partially implemented. MCP23017 support for configurable Raspberry Pi QuickMedia buttons is implemented; other I2C devices remain future work.

## Goals

- Add I2C devices to the existing Raspberry Pi setup, starting with a GPIO expander for QuickMedia buttons. Configure one active button provider at a time.
- Configure hardware through files so users do not need to modify code.
- Allow for battery monitoring and motion sensing later. See [buttons](buttons.md), [battery monitoring](battery-monitoring.md), and [motion playback](motion-playback.md).
- Protect playlist and QuickMedia reliability. Hardware is optional and sensor failures must not stop playback.

## Proposed approach

- Select the MCP23017 as a specific supported device type and configure its button pins and interrupt pins.
- Map configured chip types to supported `Iot.Device.Bindings` drivers. A binding's existence alone does not automatically make every device usable: integration and testing are still needed.
- The project references `Iot.Device.Bindings` and `System.Device.Gpio` 4.2.0.
- Multiple devices can share the physical bus, but each needs its own address and device instance. Default addresses for the intended devices do not conflict: MCP23017 `0x20`, INA219 `0x40`, MPU-6050 `0x68`.
- `buttonsconfig.json` selects either `PiGpio` or `Mcp23017`. For the MCP23017, I2C bus 1 and address `0x20` are fixed; the two interrupt pins are configured in IA, IB order.
- Preserve desktop development and mocks. Validate configuration and report problems clearly without overwriting user configuration or preventing the playlist from running.

## Hardware and reliability

- On the Pi, GPIO 2 and 3 are normally SDA and SCL. Confirm selected button and interrupt pins are available with the actual HAT and other peripherals.
- Pi-facing I2C and GPIO signals must be compatible with 3.3 V. Check breakout power, pull-ups, grounds, bus capacitance and wiring for the actual boards.
- A stuck or faulty shared bus can affect multiple devices, including buttons. Avoid blocking the playback loop; investigate fault isolation, recovery and practical bus load when implementing.

## Open questions

- Runtime recovery behaviour for an I2C bus or expander failure.
- Which additional expander chips are worth integrating and testing.

References: [Adafruit MCP23017 pinout](https://learn.adafruit.com/adafruit-mcp23017-i2c-gpio-expander/pinouts), [INA219 pinout](https://learn.adafruit.com/adafruit-ina219-current-sensor-breakout/pinouts), [MPU-6050 pinout](https://learn.adafruit.com/mpu6050-6-dof-accelerometer-and-gyro/pinouts).
