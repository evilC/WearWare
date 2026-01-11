using WearWare.Common.Media;
using WearWare.Config;

public static class QuickMediaImageEndpoints
{
    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/quickmedia-images/{buttonNumber}/{sourceFileName}", (int buttonNumber, string sourceFileName, QuickMediaService quickMediaService) =>
        {
            var imagePath = Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString(), sourceFileName);
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