using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace WearWare.Services.Physics.Simulations.Medallion
{
    internal sealed class MedallionArtwork
    {
        private const int MaskScale = 4;
        private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";
        private readonly Mask _body;
        private readonly Mask _loop;
        private readonly Vector2 _center;
        private readonly float _radius;

        public float AttachmentDistance { get; }
        public float BodyReach { get; }
        public float LoopReach { get; }

        private MedallionArtwork(Mask body, Mask loop, Mask hole)
        {
            _body = body;
            _loop = loop;
            _center = body.Center;
            _radius = Math.Max(body.Width, body.Height) / 2;
            var attachment = hole.Center;
            if (MathF.Abs(attachment.X - _center.X) > _radius * 0.02f || attachment.Y >= _center.Y)
                throw new InvalidDataException("The loop hole must be centered above the pendant body.");

            AttachmentDistance = (_center.Y - attachment.Y) / _radius;
            BodyReach = body.ReachFrom(_center) / _radius;
            LoopReach = loop.ReachFrom(attachment) / _radius;
        }

        public static MedallionArtwork Load(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var document = XDocument.Load(reader);
            var root = document.Root;
            if (root?.Name != SvgNamespace + "svg")
                throw new InvalidDataException($"Invalid medallion SVG '{path}'.");

            var values = ((string?)root.Attribute("viewBox"))?.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (values?.Length != 4 ||
                !float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
                !float.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var height) ||
                !float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0 ||
                width > 1024 || height > 1024)
                throw new InvalidDataException($"Invalid viewBox in medallion SVG '{path}'.");

            root.SetAttributeValue("width", width.ToString(CultureInfo.InvariantCulture));
            root.SetAttributeValue("height", height.ToString(CultureInfo.InvariantCulture));
            return new MedallionArtwork(
                RenderLayer(document, "body", width, height),
                RenderLayer(document, "loop", width, height),
                RenderLayer(document, "loop-hole", width, height));
        }

        public bool IsMetal(float x, float y, bool loop)
        {
            var maskX = (int)MathF.Floor((x * _radius + _center.X) * MaskScale);
            var maskY = (int)MathF.Floor((y * _radius + _center.Y) * MaskScale);
            return (loop ? _loop : _body).Contains(maskX, maskY);
        }

        private static Mask RenderLayer(XDocument source, string groupId, float width, float height)
        {
            var document = new XDocument(source);
            var selected = document.Descendants(SvgNamespace + "g")
                .Where(group => (string?)group.Attribute("id") == groupId)
                .ToArray();
            if (selected.Length != 1)
                throw new InvalidDataException($"Medallion SVG must contain exactly one '{groupId}' group.");
            for (var child = selected[0]; child.Parent != null; child = child.Parent)
            {
                foreach (var sibling in child.Parent.Elements().ToArray())
                {
                    if (sibling != child && sibling.Name != SvgNamespace + "defs" &&
                        sibling.Name != SvgNamespace + "style")
                        sibling.Remove();
                }
            }
            selected[0].SetAttributeValue("style", $"{(string?)selected[0].Attribute("style")};display:inline");

            using var stream = new MemoryStream();
            document.Save(stream);
            stream.Position = 0;
            using var svg = new SKSvg();
            var picture = svg.Load(stream) ?? throw new InvalidDataException($"Could not render SVG group '{groupId}'.");
            var pixelWidth = checked((int)MathF.Ceiling(width * MaskScale));
            var pixelHeight = checked((int)MathF.Ceiling(height * MaskScale));
            using var bitmap = new SKBitmap(pixelWidth, pixelHeight);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(MaskScale);
            canvas.DrawPicture(picture);
            canvas.Flush();

            var pixels = new bool[checked(pixelWidth * pixelHeight)];
            var left = pixelWidth;
            var right = -1;
            var top = pixelHeight;
            var bottom = -1;
            for (var y = 0; y < pixelHeight; y++)
            {
                for (var x = 0; x < pixelWidth; x++)
                {
                    if (bitmap.GetPixel(x, y).Alpha < 128)
                        continue;
                    pixels[y * pixelWidth + x] = true;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
            if (right < left || bottom < top)
                throw new InvalidDataException($"Medallion SVG group '{groupId}' is empty.");
            return new Mask(pixels, pixelWidth, pixelHeight, left, top, right, bottom);
        }

        private sealed class Mask(bool[] pixels, int pixelWidth, int pixelHeight, int left, int top, int right, int bottom)
        {
            public Vector2 Center => new((left + right + 1f) / (2 * MaskScale), (top + bottom + 1f) / (2 * MaskScale));
            public float Width => (right - left + 1f) / MaskScale;
            public float Height => (bottom - top + 1f) / MaskScale;

            public bool Contains(int x, int y) =>
                x >= 0 && x < pixelWidth && y >= 0 && y < pixelHeight && pixels[y * pixelWidth + x];

            public float ReachFrom(Vector2 origin)
            {
                var dx = Math.Max(MathF.Abs(left / (float)MaskScale - origin.X), MathF.Abs((right + 1f) / MaskScale - origin.X));
                var dy = Math.Max(MathF.Abs(top / (float)MaskScale - origin.Y), MathF.Abs((bottom + 1f) / MaskScale - origin.Y));
                return MathF.Sqrt(dx * dx + dy * dy);
            }
        }
    }
}
