namespace WearWare.Architecture.Display
{
    /// <summary>
    /// Controls access to the shared matrix display resource.
    /// Arbitrates between handlers and coordinates handoff during mode transitions.
    /// Only one handler may have control of the matrix at a time.
    /// </summary>
    public interface IMatrixController
    {
        /// <summary>
        /// Requests exclusive control of the matrix display.
        /// If control is already held, the requesting handler must wait for the current
        /// holder to release control before this returns.
        /// </summary>
        /// <param name="requestingHandler">The handler requesting control. Used for logging/diagnostics.</param>
        /// <param name="cancellationToken">Can be cancelled to abort the control request.</param>
        /// <returns>An IMatrixDisplay to use until ReleasedControl is called.</returns>
        /// <exception cref="OperationCanceledException">If cancellationToken is cancelled before control is granted.</exception>
        Task<IMatrixDisplay> RequestControlAsync(IHandler requestingHandler, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases exclusive control of the matrix display.
        /// Must be called by the handler that currently holds control.
        /// </summary>
        /// <param name="releasingHandler">The handler releasing control.</param>
        /// <exception cref="InvalidOperationException">If releasingHandler does not currently hold control.</exception>
        void ReleaseControl(IHandler releasingHandler);

        /// <summary>
        /// Gets the handler that currently holds exclusive control of the matrix, or null if none.
        /// </summary>
        IHandler? CurrentController { get; }

        /// <summary>
        /// Fired when the controller of the matrix changes.
        /// </summary>
        event Action? ControllerChanged;
    }
}
