namespace WearWare.Services.Environment
{
    // Allows the nav menu to detect the environment and show/hide the Mocks page
    public class EnvironmentService
    {
        public string EnvironmentName { get; init; }

        public EnvironmentService(string environmentName)
        {
            EnvironmentName = environmentName;
        }
    }
}