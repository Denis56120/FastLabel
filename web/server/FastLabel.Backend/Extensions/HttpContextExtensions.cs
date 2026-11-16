namespace FastLabel.Backend.Extensions;

public static class HttpContextExtensions
{
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
}