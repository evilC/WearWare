namespace WearWare.Config
{
    public static class PathConfig
    {
        public static readonly string Root;
        public static readonly string ConfigPath;
        public static readonly string IncomingPath;
        public static readonly string IncomingFolder = "incoming";
        public static readonly string LibraryPath;
        public static readonly string LibraryFolder = "library";
        public static readonly string PlaylistPath;
        public static readonly string PlaylistFolder = "playlists";
        public static readonly string QuickMediaPath;
        public static readonly string QuickMediaFolder = "quickmedia";
        public static readonly string ToolsPath;
        public static readonly string LogPath;

        static PathConfig()
        {
            var exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? throw new InvalidOperationException("Could not determine executable path.");
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (env == "Desktop")
                Root = Path.GetFullPath(Path.Combine(exePath, "..", "..", ".."));
            else
                Root = Path.GetFullPath(Path.Combine(exePath, ".."));

            ConfigPath = Path.Combine(Root, "config");
            IncomingPath = Path.Combine(Root, IncomingFolder);
            LibraryPath = Path.Combine(Root, LibraryFolder);
            PlaylistPath = Path.Combine(Root, PlaylistFolder);
            QuickMediaPath = Path.Combine(Root, QuickMediaFolder);
            ToolsPath = Path.Combine(Root, "tools");
            LogPath = Path.Combine(Root, "logs");
        }
    }
}