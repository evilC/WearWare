# Battery monitoring

Status: Future proposal, not implemented. See also [I2C](i2c.md).

## Goals

- Use an INA219 high-side current sensor breakout to monitor battery voltage, with current and power monitoring also possible.
- Consider logging and a simple UI display without making playback dependent on the sensor.
- The author's battery is a 4S pack of 21700 cells. For standard cells charged to 4.2 V each, the pack reaches 16.8 V.

## Considerations

- The INA219's stated sensing limit is 26 V. A 16.8 V pack fits, but the binding must use its 32 V measurement range rather than the 16 V range. The 32 V setting does not increase the chip's 26 V sensing limit.
- The Adafruit breakout has a 0.1-ohm shunt and a stated measurement range of +/-3.2 A. Confirm the actual board, peak battery-side current, shunt heating and voltage drop before putting it in the power path. Calibrate for the actual shunt and range.
- Measurement location determines which loads are included and whether the reading represents battery or regulated output voltage. Check wiring, common ground and transients.
- Voltage under load is not an exact state-of-charge or remaining-runtime indicator.
- A sensor failure must be reported without stopping playback; shared-bus failures may also affect buttons.

## Open questions

- Actual peak and continuous battery-side current, board suitability and safe measurement location.
- Sampling interval, logging, UI presentation and whether any battery warning is wanted.

References: [Adafruit INA219 overview](https://learn.adafruit.com/adafruit-ina219-current-sensor-breakout/overview), [INA219 binding](https://github.com/dotnet/iot/blob/v4.1.0/src/devices/Ina219/README.md).
