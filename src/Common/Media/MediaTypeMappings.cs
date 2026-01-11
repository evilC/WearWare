namespace WearWare.Common.Media
{
    public static class MediaTypeMappings
    {
        public record MediaTypeInfo(MediaType MediaType, string MimeType);

        public static readonly Dictionary<string, MediaTypeInfo> ExtensionInfo = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new(MediaType.IMAGE, "image/jpeg"),
            [".jpeg"] = new(MediaType.IMAGE, "image/jpeg"),
            [".png"] = new(MediaType.IMAGE, "image/png"),
            [".bmp"] = new(MediaType.IMAGE, "image/bmp"),
            [".gif"] = new(MediaType.ANIMATION, "image/gif")
        };

        public static MediaType? GetMediaType(string extension)
        {
            if (ExtensionInfo.TryGetValue(extension, out var info))
                return info.MediaType;
            return null;
        }

        public static string GetMimeType(string extension)
        {
            if (ExtensionInfo.TryGetValue(extension, out var info))
                return info.MimeType;
            return "application/octet-stream";
        }
    }
}
