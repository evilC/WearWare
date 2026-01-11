using Microsoft.AspNetCore.Mvc;
using WearWare.Common.Media;
using WearWare.Config;

namespace WearWare.Components.Endpoints;

// The purpose of this endpoint is to allow the client UI to access files which are outside of the wwwroot folder
public static class LibraryImageEndpoints
{
    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/library-image/{fileName}", ([FromRoute] string fileName) =>
        {
            var filePath = Path.Combine(PathConfig.LibraryPath, fileName);
            if (!File.Exists(filePath))
                return Results.NotFound();
            var contentType = GetContentType(filePath);
            var stream = File.OpenRead(filePath);
            return Results.File(stream, contentType);
        });
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path);
        return MediaTypeMappings.GetMimeType(ext);
    }
}
