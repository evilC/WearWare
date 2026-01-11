// ...existing code...
using WearWare.Components;
using WearWare.Components.Endpoints;
using WearWare.Services.Import;
using WearWare.Services.Library;
using WearWare.Services.MediaController;
using WearWare.Services.Playlist;
using WearWare.Services.QuickMedia;
using WearWare.Services.Mocks;
using WearWare.Config;
using System.Net;

using Serilog;
using Serilog.Events;
using WearWare.Services.Logging;
using WearWare.Utils;
using WearWare.Services.ShutdownService;
using WearWare.Services.MatrixConfig;
using WearWare.Services.StreamConverter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<ShutdownService>();
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Configure Serilog
var logDir = PathConfig.LogPath;
var logFilePath = Path.Combine(logDir, "log.txt");
Directory.CreateDirectory(logDir);

// Create and register the custom in-memory sink
var blazorInMemorySink = new BlazorInMemoryLogSink();
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        logFilePath,
        restrictedToMinimumLevel: LogEventLevel.Information,
        fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 5
    )
    .WriteTo.Sink(blazorInMemorySink, restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();
Log.Information("{sep} Starting on {Environment} {sep}", LogTools.HeaderHeadTail, Dns.GetHostName(), LogTools.HeaderHeadTail);

Directory.CreateDirectory(PathConfig.ConfigPath);
var appConfigFile = Path.Combine(PathConfig.ConfigPath, "appconfig.json");
AppConfig appConfig;
AppConfig? loadedConfig = null;
try {
    if (File.Exists(appConfigFile))
    {
        loadedConfig = JsonUtils.FromJsonFile<AppConfig>(appConfigFile);
        if (loadedConfig != null)
        {
            Log.Information("Loaded app config from {ConfigFile}", appConfigFile);
        }
        else
        {
            Log.Warning("Tried to load {ConfigFile}, but it was invalid, re-creating...", appConfigFile);
        }
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Error loading app config file {ConfigFile}, re-creating...", appConfigFile);
}
if (loadedConfig == null)
{
    loadedConfig = new AppConfig();
    JsonUtils.ToJsonFile(appConfigFile, loadedConfig);
    Log.Information("Created default app config file at {ConfigFile}", appConfigFile);
}
appConfig = loadedConfig;

builder.Services.AddHttpClient();
builder.Services.AddSingleton(blazorInMemorySink);
builder.Services.AddSingleton<MatrixConfigService>();
builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton<PlaylistService>();
builder.Services.AddSingleton<InMemoryLogService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddSingleton<LibraryService>();

var env = builder.Environment.EnvironmentName;
if (env == "Desktop")
{
    // Desktop specific services (mocks)
    builder.Services.AddSingleton<IStreamConverterService, MockStreamConverterService>();
    builder.Services.AddSingleton<IStreamPlayer, MockStreamPlayer>();
    builder.Services.AddSingleton<IQuickMediaButtonFactory, MockQuickMediaButtonFactory>();
}
else
{
    // RPi specific services
    builder.Services.AddSingleton<IStreamConverterService, StreamConverterService>();
    builder.Services.AddSingleton<IStreamPlayer, RpiStreamPlayer>();
    builder.Services.AddSingleton<IQuickMediaButtonFactory, QuickMediaGpioButtonFactory>();
}
builder.Services.AddSingleton<MediaControllerService>();
builder.Services.AddSingleton<QuickMediaService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "desktop")
// {
//     app.UseDeveloperExceptionPage();
// }
// else
// {
//     app.UseExceptionHandler("/Error");
//     // app.UseHsts(); // Not needed if you never use HTTPS
// }

app.UseStaticFiles();
app.UseAntiforgery();


// Register incoming media endpoints
IncomingMediaEndpoints.MapEndpoints(app);
// Register library image endpoints
LibraryImageEndpoints.MapEndpoints(app);
// Register playlist image endpoints
PlaylistImageEndpoints.MapEndpoints(app);
// Register QuickMedia image endpoints
QuickMediaImageEndpoints.MapEndpoints(app);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var quickMediaService = app.Services.GetRequiredService<QuickMediaService>();

var playlistService = app.Services.GetRequiredService<PlaylistService>();
// Load the playlist and initialize the MediaControllerService
playlistService.Initialize();
// Initializes the QuickMedia buttons
// We do this after the MediaControllerService has been started, to give floating buttons a chance to fire and be ignored
quickMediaService.Initialize();

app.Run();