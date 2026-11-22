namespace WearWare.Services
{
    /// <summary>
    /// Base interface for manager components. Managers own user-configurable rules,
    /// configuration, and persisted application state. They expose operations and
    /// information needed by the Web UI and other application components.
    /// </summary>
    public interface IManager
    {
        /// <summary>
        /// Fired when manager state has changed (configuration, selections, etc).
        /// </summary>
        event Action? StateChanged;
    }
}
