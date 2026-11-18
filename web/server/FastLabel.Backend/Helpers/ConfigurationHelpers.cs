namespace FastLabel.Backend.Helpers;

public static class ConfigurationHelpers
{
    public static string GetClientUrl(IConfiguration configuration)
    {
        return configuration["Url:ClientUrl"] ?? "https://fastlabel.com";
    }  

    public static string GetServerUrl(IConfiguration configuration)
    {
        return configuration["Url:ServerUrl"] ?? "https://fastlabel.com";
    }  
}