using System.Text.Json;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.MediaController;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.Library
{
    public class LibraryService
    {
        public event Action? ItemsChanged;
        private readonly SortedList<string, PlayableItem> _items = [];

        public IReadOnlyList<PlayableItem> Items
        {
            get
            {
                return _items.Values.ToList();
            }
        }

        private readonly ILogger<LibraryService> _logger;
        private readonly MediaControllerService _mediaControllerService;
        private readonly MatrixConfigService _matrixConfigService;

        public LibraryService(ILogger<LibraryService> logger, MediaControllerService mediaControllerService, MatrixConfigService matrixConfigService)
        {
            _logger = logger;
            _mediaControllerService = mediaControllerService;
            LoadLibraryItems();
            _matrixConfigService = matrixConfigService;
            _logger.LogInformation("LibraryService initialized.");
        }

        private void LoadLibraryItems()
        {
            _items.Clear();
            if (!Directory.Exists(PathConfig.LibraryPath))
                return;

            var jsonFiles = Directory.EnumerateFiles(PathConfig.LibraryPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in jsonFiles)
            {
                PlayableItem? libraryItem;
                try
                {
                    var json = File.ReadAllText(file);
                    libraryItem = JsonSerializer.Deserialize<PlayableItem>(json);
                }
                catch
                {
                    // Optionally log or handle errors
                    continue;
                }
                
                if (libraryItem != null)
                {
                    // Older JSON may not include MatrixOptions; ensure it's initialized so code relying on it won't see null.
                    // if (libraryItem.MatrixOptions == null)
                    //     libraryItem.MatrixOptions = _matrixConfigService.CloneOptions();

                    _items.Add(libraryItem.Name, libraryItem);
                }
            }
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
        public void DeleteLibraryItem(PlayableItem item)
        {
            try
            {
                var jsonPath = Path.Combine(PathConfig.LibraryPath, $"{item.Name}.json");

                if (File.Exists(jsonPath)) File.Delete(jsonPath);
                if (File.Exists(item.GetStreamFilePath())) File.Delete(item.GetStreamFilePath());
                if (File.Exists(item.GetSourceFilePath())) File.Delete(item.GetSourceFilePath());
            }
            catch
            {
                // Swallow errors - deletion failure shouldn't crash the UI.
            }
            // Reload the library to refresh the UI
            Reload();
        }

        public void PlayPreviewItem(PlayableItem libItem)
        {
            _mediaControllerService.PlayQuickMedia(libItem);
        }
    }
}
