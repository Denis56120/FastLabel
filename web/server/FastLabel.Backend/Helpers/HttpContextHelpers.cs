namespace FastLabel.Backend.Helpers;

public static class HttpContextHelpers
{
    public const string DefaultLanguage = "en";

    public static void SetLanguageCookie(this HttpContext httpContext, string langCode)
    {
        if (!string.IsNullOrEmpty(langCode))
        {
            httpContext.Response.Cookies.Append("language", langCode, new CookieOptions
            {
                HttpOnly = false,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
        }
    }

    public static string GetLanguageCookie(this HttpContext httpContext)
    {
        string language = DefaultLanguage;
        if (httpContext.Request.Cookies.TryGetValue("language", out var cookieLang) && !string.IsNullOrEmpty(cookieLang))
        {
            language = cookieLang;
        }
        return language;
    }
}