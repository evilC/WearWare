using System.Text.Json;
using WearWare.Config;

namespace WearWare.Services.Library
{
    public class LibraryService
    {
        public event Action? ItemsChanged;
        private List<LibraryItem> _items = new();
        private bool _isInitialized = false;

        public IReadOnlyList<LibraryItem> Items
        {
            get
            {
                if (!_isInitialized)
                {
                    LoadLibraryItems();
                }
                return _items;
            }
        }

        private readonly ILogger<LibraryService> _logger;

        public LibraryService(ILogger<LibraryService> logger)
        {
            _logger = logger;
            _logger.LogInformation("LibraryService initialized.");
        }

        private void LoadLibraryItems()
        {
            _items.Clear();
            _isInitialized = false;
            if (!Directory.Exists(PathConfig.LibraryPath))
                return;

            var jsonFiles = Directory.EnumerateFiles(PathConfig.LibraryPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in jsonFiles)
            {
                LibraryItem? item = null;
                try
                {
                    var json = File.ReadAllText(file);
                    item = JsonSerializer.Deserialize<LibraryItem>(json);
                }
                catch
                {
                    // Optionally log or handle errors
                    continue;
                }
                if (item != null)
                    _items.Add(item);
            }
            // Sort items alphabetically by name (case-insensitive) for consistent ordering
            _items = _items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
            _isInitialized = true;
            ItemsChanged?.Invoke();
        }

        /// <summary>
        /// Reloads library items from disk and notifies listeners.
        /// </summary>
        public void Reload()
        {
            LoadLibraryItems();
        }

            /// <summary>
            /// Deletes the specified library item files and metadata, then reloads the library.
            /// </summary>
            /// <param name="item"></param>
            public void DeleteLibraryItem(LibraryItem item)
            {
                try
                {
                    var jsonPath = Path.Combine(PathConfig.LibraryPath, $"{item.Name}.json");
                    var streamPath = Path.Combine(PathConfig.LibraryPath, $"{item.Name}.stream");
                    var sourcePath = Path.Combine(PathConfig.LibraryPath, item.SourceFileName ?? "");

                    if (File.Exists(jsonPath)) File.Delete(jsonPath);
                    if (File.Exists(streamPath)) File.Delete(streamPath);
                    if (!string.IsNullOrEmpty(item.SourceFileName) && File.Exists(sourcePath)) File.Delete(sourcePath);
                }
                catch
                {
                    // Swallow errors - deletion failure shouldn't crash the UI.
                }
                // Reload the library to refresh the UI
                Reload();
            }
    }
}
