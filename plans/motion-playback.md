# Motion-driven playback

Status: Future idea, not implemented. See also [I2C](i2c.md).

## Goals

- Use an Adafruit MPU-6050 accelerometer and gyroscope for future graphical physics simulations, such as a ball responding to movement of the matrix panel.
- Explore additional playlist and QuickMedia modes without compromising normal media playback.
- Configure the device through files. The existing `Iot.Device.Bindings` package includes an MPU-6050 binding.

## Considerations

- Polling the sensor is a possible approach and does not require an additional interrupt GPIO. Choose a suitable sample rate based on responsiveness, bus load and power use.
- Mounting orientation, calibration and filtering will affect how motion maps to the display.
- Sensor errors or unavailable hardware must not stop ordinary playback. A shared I2C bus fault could also affect buttons.

## Open questions

- How simulations fit into the existing player and playlist/QuickMedia model, and what should happen when the sensor is unavailable.
- Sensor configuration, sampling rate, calibration and user-facing controls.

Reference: [Adafruit MPU-6050 pinout](https://learn.adafruit.com/mpu6050-6-dof-accelerometer-and-gyro/pinouts).
