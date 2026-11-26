using System.Numerics;
using WearWare.Services.Matrix;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Physics.Imu;

namespace WearWare.Services.Physics.Simulations.Medallion
{
    public sealed class MedallionRenderer : NecklaceRenderer
    {
        public MedallionRenderer(ImuService imu, IMatrixDisplay display, MatrixConfigService config, ILogger<MedallionRenderer> logger)
            : base(imu, display, config, logger)
        {
        }

        protected override void DrawBody(Vector2 center, float radius, float angle) =>
            DrawArtwork(center, radius, angle, MedallionArtwork.Part.Body);
    }
}
