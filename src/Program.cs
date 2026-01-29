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
using WearWare.Services.ShutdownService;
using WearWare.Services.MatrixConfig;
using WearWare.Services.StreamConverter;
using WearWare.Services.OperationProgress;
using WearWare.Services.Environment;

var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureHostOptions(o => o.ShutdownTimeout = TimeSpan.FromSeconds(1)); // Forces app to shut down within 1 second
builder.Services.AddHostedService<ShutdownService>();
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Create folders
Directory.CreateDirectory(PathConfig.ConfigPath);
Directory.CreateDirectory(PathConfig.IncomingPath);
Directory.CreateDirectory(PathConfig.LibraryPath);
Directory.CreateDirectory(PathConfig.PlaylistPath);
Directory.CreateDirectory(PathConfig.QuickMediaPath);
Directory.CreateDirectory(PathConfig.LogPath);
Directory.CreateDirectory(PathConfig.ToolsPath);

// Configure Serilog
var logFilePath = Path.Combine(PathConfig.LogPath, "log.txt");

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


builder.Services.AddHttpClient();
builder.Services.AddSingleton(blazorInMemorySink);
builder.Services.AddSingleton<MatrixConfigService>();
builder.Services.AddSingleton<PlaylistService>();
builder.Services.AddSingleton<InMemoryLogService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddSingleton<LibraryService>();

var env = builder.Environment.EnvironmentName;
builder.Services.AddSingleton(sp =>{return new EnvironmentService(env);});  // Allow Nav Menu to detect if running on Desktop or RPi

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
    if (!File.Exists(Path.Combine(PathConfig.ToolsPath, "led-image-viewer")))
    {
        Log.Warning("led-image-viewer not found in tools folder! Stream conversion functionality will not work!");
    }
    builder.Services.AddSingleton<IStreamConverterService, StreamConverterService>();
    builder.Services.AddSingleton<IStreamPlayer, RpiStreamPlayer>();
    builder.Services.AddSingleton<IQuickMediaButtonFactory, QuickMediaGpioButtonFactory>();
}
builder.Services.AddSingleton<MediaControllerService>();
builder.Services.AddSingleton<QuickMediaService>();
builder.Services.AddSingleton<IOperationProgressService, OperationProgressService>();

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


// Load the playlist and initialize the MediaControllerService
// This will cause the MediaController to start playing the playlist
// If we do not do this, then the MediaControllerSercie will not start playing until we visit the Playlist page
var playlistService = app.Services.GetRequiredService<PlaylistService>();
playlistService.Initialize();

// Initialize the QuickMedia buttons
// Without this, QuickMedia buttons will not be enabled until we visit the QuickMedia page
var quickMediaService = app.Services.GetRequiredService<QuickMediaService>();
quickMediaService.Initialize();

app.Run();