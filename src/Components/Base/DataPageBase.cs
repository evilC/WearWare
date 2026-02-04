using Microsoft.AspNetCore.Components;

namespace WearWare.Components.Base
{
    /// <summary>
    /// Base component that standardises the "start loading early, wait until interactive" pattern.
    /// Derive pages that need to hide UI until data + circuit are ready and implement
    /// <see cref="EnsureDataLoadedAsync"/> to start or perform any data preparation.
    /// </summary>
    public abstract class DataPageBase : ComponentBase, IDisposable
    {
        private Task? _dataLoadTask;
        private bool _ready;

        /// <summary>
        /// True once the circuit is interactive and the data load task (if any) has completed.
        /// </summary>
        protected bool Ready => _ready;

        // In your OnInitialized override:
        // 1) Subscribe to any events needed to know when data changes.
        // 2) Do not Fetch data
        // 3) Call base.OnInitialized() to kick off the data load task and interactive wait.
        protected override void OnInitialized()
        {
            // Start the data load early (non-blocking). Derived types implement EnsureDataLoadedAsync.
            if (_dataLoadTask == null)
                _dataLoadTask = EnsureDataLoadedAsync();

            base.OnInitialized();
        }

        /// <summary>
        /// Implement this to perform any data initialisation. If the work is synchronous,
        /// just set up fields and return Task.CompletedTask. If later you need async work, await it here.
        /// </summary>
        protected abstract Task EnsureDataLoadedAsync();

        /// <summary>
        /// Default Dispose does nothing; derived pages should unsubscribe from events and then call base.Dispose().
        /// </summary>
        public virtual void Dispose()
        {
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                if (_dataLoadTask != null)
                {
                    try
                    {
                        await _dataLoadTask;
                    }
                    catch (Exception)
                    {
                        // Derived pages may inspect service state and show errors.
                        // Swallow here to avoid breaking the render pipeline.
                    }
                }
                // Thread.Sleep(5000); // Simulate long load for testing
                _ready = true;
                StateHasChanged();
            }

            await base.OnAfterRenderAsync(firstRender);
        }
    }
}
