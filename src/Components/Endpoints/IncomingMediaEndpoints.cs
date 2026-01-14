using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.Import;
using WearWare.Utils;

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

            // Upload endpoint: accepts multipart/form-data with a single file field named "file"
            app.MapPost("/incoming-media/upload", async (HttpRequest request, ImportService importService) =>
            {
                if (!request.HasFormContentType)
                    return Results.BadRequest("Form content expected");

                var form = await request.ReadFormAsync();
                var file = form.Files.GetFile("file");
                if (file == null)
                    return Results.BadRequest("No file provided");

                var incomingDir = PathConfig.IncomingPath;
                Directory.CreateDirectory(incomingDir);

                var originalFileName = Path.GetFileName(file.FileName);
                var ext = Path.GetExtension(originalFileName);
                if (string.IsNullOrEmpty(ext) || !MediaTypeMappings.ExtensionInfo.ContainsKey(ext))
                    return Results.BadRequest("Unsupported file type");

                var sanitizedBase = FilenameValidator.Sanitize(Path.GetFileNameWithoutExtension(originalFileName));
                var destFileName = sanitizedBase + ext;
                var destPath = Path.Combine(incomingDir, destFileName);

                try
                {
                    using var fs = File.Create(destPath);
                    await file.CopyToAsync(fs);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }

                // Notify import service so UI updates
                try { importService.NotifyStateChanged(); } catch { }

                return Results.Ok(new { filename = destFileName });
            });
    }
}
