namespace WearWare.Services.Simulations
{
    public interface IPhysicsRenderer
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}
