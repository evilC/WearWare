using WearWare.Architecture;

namespace WearWare.Architecture.Physics.Simulations
{
    public interface IPhysicsRenderer : IHandler
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}
