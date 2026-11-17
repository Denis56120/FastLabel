namespace FastLabel.Backend.Tests.Extensions;

public static class ServiceExtensions
{
    public static void AddEnvironmentConfiguration()
    {
        var fileName = ".env.test";
        
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }
        else
        {
            var parentDir = Directory.GetParent(Directory.GetCurrentDirectory());
            while (parentDir != null)
            {
                var candidate = Path.Combine(parentDir.FullName, fileName);
                if (File.Exists(candidate))
                {
                    Env.Load(candidate);
                    break;
                }
                parentDir = parentDir.Parent;
            }
        }
    }
}