namespace WearWare.Config
{
    public static class PathConfig
    {
        private static readonly string _root;
        public static readonly string ConfigPath;
        public static readonly string IncomingPath;
        public static readonly string LibraryPath;
        public static readonly string PlaylistPath;
        public static readonly string QuickMediaPath;
        public static readonly string ToolsPath;
        public static readonly string LogPath;

        static PathConfig()
        {
            var exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? throw new InvalidOperationException("Could not determine executable path.");
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (env == "Desktop")
                _root = Path.GetFullPath(Path.Combine(exePath, "..", "..", ".."));
            else
                _root = Path.GetFullPath(Path.Combine(exePath, ".."));

            ConfigPath = Path.Combine(_root, "config");
            IncomingPath = Path.Combine(_root, "incoming");
            LibraryPath = Path.Combine(_root, "library");
            PlaylistPath = Path.Combine(_root, "playlists");
            QuickMediaPath = Path.Combine(_root, "quickmedia");
            ToolsPath = Path.Combine(_root, "tools");
            LogPath = Path.Combine(_root, "logs");
        }
    }
}