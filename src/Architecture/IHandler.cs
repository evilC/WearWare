namespace WearWare.Architecture
{
    /// <summary>
    /// Base interface for handler components. A Handler applies configuration at runtime to do work.
    /// Handlers respond to logical input, produce output on the matrix, and coordinate state transitions.
    /// </summary>
    public interface IHandler
    {
        /// <summary>
        /// Gets the name/identifier for this handler.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the current state of the handler (e.g., running, idle, failed).
        /// </summary>
        HandlerState State { get; }

        /// <summary>
        /// Fired when the handler state changes.
        /// </summary>
        event Action? StateChanged;
    }

    /// <summary>
    /// Runtime state of a handler.
    /// </summary>
    public enum HandlerState
    {
        /// <summary>Handler is initialized but not running.</summary>
        Idle,

        /// <summary>Handler is actively producing output on the matrix.</summary>
        Running,

        /// <summary>Handler encountered an error.</summary>
        Failed,
    }
}
