using WearWare.Services;

namespace WearWare.Services.Physics.Simulations
{
    public interface IPhysicsRenderer : IHandler
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}
