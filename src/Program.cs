using WearWare.Components;
using WearWare.Components.Endpoints;
using WearWare.Services.Import;
using WearWare.Services.Library;
using WearWare.Services.Mocks;
using System.Net;

using Serilog;
using Serilog.Events;
using WearWare.Services.Logging;
using WearWare.Services.MatrixConfig;
using WearWare.Services.StreamConverter;
using WearWare.Services.OperationProgress;
using WearWare.Services.Environment;
using WearWare.Services.Options;
using WearWare.Services.TempMon;
using WearWare.Services.Matrix;
using WearWare.Services.Playlist;
using WearWare.Services.QuickMedia;
using WearWare.Architecture.Mode;
using WearWare.Services.Button;
using WearWare.Services.Physics;
using WearWare.Services.Physics.Imu;
using WearWare.Services.Physics.Imu.Sensors;
using WearWare.Services.Playback;

var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureHostOptions(o => o.ShutdownTimeout = TimeSpan.FromSeconds(1)); // Forces app to shut down within 1 second
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
builder.Services.AddSingleton<AppOptionsService>();
builder.Services.AddSingleton<MatrixConfigService>();
builder.Services.AddSingleton<InMemoryLogService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddSingleton<LibraryService>();
builder.Services.AddSingleton<PlaylistManager>();
builder.Services.AddSingleton<QuickMediaManager>();

var env = builder.Environment.EnvironmentName;
builder.Services.AddSingleton(sp =>{return new EnvironmentService(env);});  // Allow Nav Menu to detect if running on Desktop or RPi
builder.Services.AddSingleton(sp => new ImuService(
    sp.GetRequiredService<ILogger<ImuService>>(),
    env == "Desktop" ? null : () => new Mpu6050Sensor()));
builder.Services.AddHostedService(sp => sp.GetRequiredService<ImuService>());

if (env == "Desktop")
{
    // Desktop specific services (mocks)
    builder.Services.AddSingleton<IMediaConverterService, MockMediaConverterService>();
    builder.Services.AddSingleton<IMediaPlayer, MockMediaPlayer>();
    // builder.Services.AddSingleton<IQuickMediaButtonFactory, MockQuickMediaButtonFactory>();
    builder.Services.AddSingleton<ITempMonService, MockTempMonService>();
    builder.Services.AddSingleton<MatrixDisplay>();
    builder.Services.AddSingleton<IMatrixDisplay>(sp => sp.GetRequiredService<MatrixDisplay>());
}
else
{
    // RPi specific services
    if (!File.Exists(Path.Combine(PathConfig.ToolsPath, "frame-sequence-player")))
    {
        Log.Warning("frame-sequence-player not found in tools folder! FSEQ conversion functionality will not work!");
    }
    builder.Services.AddSingleton<IMediaConverterService, FseqConverterService>();
    builder.Services.AddSingleton<MatrixDisplay>();
    builder.Services.AddSingleton<IMatrixDisplay>(sp => sp.GetRequiredService<MatrixDisplay>());
    builder.Services.AddSingleton<IMediaPlayer, RpiMediaPlayer>();
    builder.Services.AddSingleton<ITempMonService, TempMonService>();
}

builder.Services.AddSingleton<PlaylistHandler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PlaylistHandler>());
builder.Services.AddSingleton<ButtonManager>();
builder.Services.AddSingleton<ButtonFunctionMappingService>();
builder.Services.AddSingleton<QuickMediaHandler>();
builder.Services.AddSingleton<ModeManager>();
builder.Services.AddSingleton<PhysicsHandler>();
builder.Services.AddSingleton<ModeHandler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ModeHandler>());
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
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// app.UseHttpsRedirection();

app.UseAntiforgery();


// Register incoming media endpoints
IncomingMediaEndpoints.MapEndpoints(app);
// Register library image endpoints
LibraryImageEndpoints.MapEndpoints(app);
// Register playlist image endpoints
PlaylistImageEndpoints.MapEndpoints(app);
// Register QuickMedia image endpoints
QuickMediaImageEndpoints.MapEndpoints(app);

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Wire button manager before anything which depends on button events
app.Services.GetRequiredService<ButtonManager>().Initialize();
// Wire mode handling before enabling button events
app.Services.GetRequiredService<ModeHandler>();
app.Services.GetRequiredService<QuickMediaHandler>().Initialize();

app.Run();