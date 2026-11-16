namespace FastLabel.Backend.Tests.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddEnvironmentConfiguration(this IServiceCollection services)
    {
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env.testing");

        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }

        return services;
    }
}