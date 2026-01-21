using System.Text.Json;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.MediaController;

namespace WearWare.Services.Library
{
    public class LibraryService
    {
        public event Action? ItemsChanged;
        private readonly SortedList<string, LibraryItem> _items = [];
        private readonly Dictionary<string, PlayableItem> _previewItems = [];

        public IReadOnlyList<LibraryItem> Items
        {
            get
            {
                return _items.Values.ToList();
            }
        }

        private readonly ILogger<LibraryService> _logger;
        private readonly MediaControllerService _mediaControllerService;

        public LibraryService(ILogger<LibraryService> logger, MediaControllerService mediaControllerService)
        {
            _logger = logger;
            _mediaControllerService = mediaControllerService;
            LoadLibraryItems();
            _logger.LogInformation("LibraryService initialized.");
        }

        private void LoadLibraryItems()
        {
            _items.Clear();
            _previewItems.Clear();
            if (!Directory.Exists(PathConfig.LibraryPath))
                return;

            var jsonFiles = Directory.EnumerateFiles(PathConfig.LibraryPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in jsonFiles)
            {
                LibraryItem? libraryItem;
                try
                {
                    var json = File.ReadAllText(file);
                    libraryItem = JsonSerializer.Deserialize<LibraryItem>(json);
                }
                catch
                {
                    // Optionally log or handle errors
                    continue;
                }
                // Add dummy PlayableItem for previewing
                if (libraryItem != null){
                    _items.Add(libraryItem.Name, libraryItem);
                    _previewItems.Add(libraryItem.Name, new PlayableItem(
                        name: libraryItem.Name,
                        parentFolder: PathConfig.LibraryPath,
                        mediaType: libraryItem.MediaType,
                        sourceFileName: libraryItem.SourceFileName,
                        playMode: PlayMode.FOREVER,
                        playModeValue: 1,
                        relativeBrightness: libraryItem.RelativeBrightness,
                        currentBrightness: libraryItem.CurrentBrightness)
                    );
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
        public void DeleteLibraryItem(LibraryItem item)
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

        public void PlayPreviewItem(LibraryItem libItem)
        {
            _previewItems.TryGetValue(libItem.Name, out var playableItem);
            if (playableItem is not null)
            {
                _mediaControllerService.PlayQuickMedia(playableItem);
            }
        }
    }
}
