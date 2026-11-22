# Physics

Status: IMU sensing and a separate Physics mode are implemented. Simulation tuning and hardware testing remain ongoing. See also [I2C](i2c.md).

## Current implementation

- The MPU-6050 uses the `Iot.Device.Bindings` driver on I2C bus 1 at the default address `0x68`. The service polls at approximately 30 Hz; it does not use a sensor interrupt.
- Raw acceleration and gyroscope readings are retained alongside mounting-adjusted readings. Tilt is calculated from adjusted acceleration and smoothed; acceleration includes movement as well as gravity.
- The selected mode is saved in `config/mode-state.json`. A saved Physics mode is retried at startup.
- The Physics page selects from the ordered list: Fluid, Ball and IMU Test. Fluid is the default. The selection is saved in `config/physics-state.json` and restored at startup.
- Changing the selection switches simulations immediately when Physics is active, without selecting Physics mode. During a QuickMedia interruption, the new selection is used when Physics resumes.
- Invalid or unreadable simulation state is logged and reported on the Physics page, and Fluid is used. If saving a selection fails, the current selection remains unchanged and the page reports the error.
- An unavailable, stale or failed IMU prevents Physics from running. WearWare falls back to Playlist and reports the failure, while Physics remains the selected mode. If Playlist is stopped, the display remains blank. A sensor failure is logged and monitoring does not automatically retry.

## Mounting orientation

- `config/imuconfig.json` is created on startup if missing, with `x`, `y` and `z` all set to `1`. Changes take effect after restarting WearWare.
- Each axis must be `1` or `-1`, with zero or two axes inverted. The signs apply to both acceleration and gyroscope readings. They support axis-aligned 180-degree rotations, not axis swaps or arbitrary rotations.
- For the author's inward-facing mount, the intended setting is `x: -1`, `y: 1`, `z: -1`. Confirm the direction on the mounted shirt.
- Invalid configuration is logged and disables IMU monitoring rather than overwriting the file. Playlist playback remains available.

## Remaining work

- Configure the sensor model and address, and consider calibration or additional filtering if needed.
- Add physical buttons to select the previous or next simulation.
- Consider display pitch when calculating the real-world fall rate, and configuration options for the simulations. The fluid simulation currently uses an off-screen margin to reduce wall-sticking artefacts, with trade-offs in visible pool depth and splashing.
- Verify mounting directions, simulation behaviour and performance on the shirt. A shared I2C bus fault can also affect buttons.

Reference: [Adafruit MPU-6050 pinout](https://learn.adafruit.com/mpu6050-6-dof-accelerometer-and-gyro/pinouts).
