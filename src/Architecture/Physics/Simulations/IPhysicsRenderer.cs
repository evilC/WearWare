namespace WearWare.Architecture.Physics.Simulations
{
    public interface IPhysicsRenderer
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}
