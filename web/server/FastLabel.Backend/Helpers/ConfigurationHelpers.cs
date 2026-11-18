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

    public static string GetStripeKey(IConfiguration configuration)
    {
        return configuration["STRIPE_SECRET_KEY"] ?? string.Empty;
    }  

    public static string GetDBName(IConfiguration configuration)
    {
        return configuration["DB_NAME"] ?? string.Empty;
    }  

    public static string GetDBUser(IConfiguration configuration)
    {
        return configuration["DB_USER"] ?? string.Empty;
    }  

    public static string GetDBPassword(IConfiguration configuration)
    {
        return configuration["DB_PASSWORD"] ?? string.Empty;
    }  

    public static string GetCookieSecurity(IConfiguration configuration)
    {
        return configuration["CookieSettings:SecurePolicy"] ?? string.Empty;
    }  
}