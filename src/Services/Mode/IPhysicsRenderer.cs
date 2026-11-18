namespace WearWare.Services.Mode
{
    public interface IPhysicsRenderer
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}
