using WearWare.Common.Media;
using WearWare.Config;

namespace WearWare.Components.Endpoints;

// The purpose of this endpoint is to allow the client UI to access files which are outside of the wwwroot folder
public static class IncomingMediaEndpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        // Serve media from ../incoming relative to the project root
        app.MapGet("/incoming-media/{filename}", (string filename, IWebHostEnvironment env) =>
        {
            var incomingDir = PathConfig.IncomingPath;
            var filePath = Path.Combine(incomingDir, filename);
            if (!File.Exists(filePath))
            {
                return Results.NotFound();
            }
            var ext = Path.GetExtension(filePath);
            var contentType = MediaTypeMappings.GetMimeType(ext);
            return Results.File(filePath, contentType);
        });
    }
}
