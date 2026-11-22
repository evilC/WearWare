using RPiRgbLEDMatrix;

namespace WearWare.Services.Matrix
{
    /// <summary>
    /// Provides synchronous access to the LED matrix for a handler that currently owns display control.
    /// The callback must complete synchronously and must not retain references to matrix or canvas.
    /// </summary>
    public interface IMatrixDisplay
    {
        /// <summary>
        /// Executes a callback with exclusive access to the matrix and canvas.
        /// The callback must complete synchronously and must not retain references.
        /// </summary>
        /// <typeparam name="T">Return type from the callback.</typeparam>
        /// <param name="action">Callback receiving matrix and canvas. Must not store references.</param>
        /// <returns>Value returned by the callback.</returns>
        T WithMatrix<T>(Func<RGBLedMatrix, RGBLedCanvas, T> action);

        /// <summary>
        /// Executes a callback with exclusive access to the matrix and canvas (void version).
        /// The callback must complete synchronously and must not retain references.
        /// </summary>
        /// <param name="action">Callback receiving matrix and canvas. Must not store references.</param>
        void WithMatrix(Action<RGBLedMatrix, RGBLedCanvas> action);

        /// <summary>
        /// Clears the matrix display.
        /// </summary>
        /// <returns>True if successful, false if matrix is not initialized.</returns>
        bool Clear();
    }
}
