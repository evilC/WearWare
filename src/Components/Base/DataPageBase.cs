
namespace WearWare.Components.Base
{
    /// <summary>
    /// Base component that standardises the "wait until interactive before loading data" pattern.
    /// Pages should derive from this class and hide UI until data + circuit are ready.
    /// We COULD start to pre-load data in OnInitialized, but this can lead to race conditions...
    /// ... unless properly handled, which just adds extra complexity for little benefit
    /// </summary>
    public abstract class DataPageBase : ComponentBase, IDisposable
    {
        private Task? _initializeTask;
        private bool _ready;

        /// <summary>
        /// True once the circuit is interactive and the data load task (if any) has completed.
        /// </summary>
        protected bool Ready => _ready;

        /// <summary>
        /// Override this to perform any data initialisation.
        /// Subscribe to services FIRST, THEN get data to avoid possibility of race conditions.
        /// </summary>
        protected abstract Task InitializeDataAsync();
        /* Template:
        {
            if (!_subscribed)
            {
                MyService.SomeEvent += OnSomeEvent;
                _subscribed = true;
            }
            myData = MyService.GetData();
        }
        */

        /// <summary>
        /// Derived classes should implement Dispose to unsubscribe from any events
        /// Not doing so will lead to memory leaks.
        /// </summary>
        public virtual void Dispose()
        {
            /* Template:
            if (_subscribed)
            {
                MyService.SomeEvent -= OnSomeEvent;
                _subscribed = false;
            }
            */
        }

        /// <summary>
        /// On first render, start the data load task and set ready = true once complete.
        /// In derived classes, use the Ready property to hide UI until this process is complete.
        /// </summary>
        /// <param name="firstRender"></param>
        /// <returns></returns>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Start the data initialization now that the circuit is interactive.
                if (_initializeTask == null)
                    _initializeTask = InitializeDataAsync();

                if (_initializeTask != null)
                {
                    try
                    {
                        await _initializeTask;
                    }
                    catch (Exception)
                    {
                        // Derived pages may inspect service state and show errors.
                        // Swallow here to avoid breaking the render pipeline.
                    }
                }

                _ready = true;
                StateHasChanged();
            }

            await base.OnAfterRenderAsync(firstRender);
        }
    }
}
