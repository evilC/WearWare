using WearWare.Config;
using WearWare.Services.Playlist;
using Microsoft.AspNetCore.Http;
using WearWare.Common.Media;

namespace WearWare.Components.Endpoints;

public static class PlaylistImageEndpoints
{
    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/playlist-images/{playlistName}/{sourceFileName}", (string playlistName, string sourceFileName, PlaylistService playlistService) =>
        {
            var imagePath = Path.Combine(PathConfig.PlaylistPath, playlistName, sourceFileName);
            if (!File.Exists(imagePath))
                return Results.NotFound();
            var ext = Path.GetExtension(imagePath);
            var contentType = GetContentType(imagePath);
            var stream = File.OpenRead(imagePath);
            return Results.File(stream, contentType);
        });
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path);
        return MediaTypeMappings.GetMimeType(ext);
    }
}
