public static class HttpContextExtensions
{
    public static string GetRequestLanguage(this HttpContext context)
    {
        var lang = context.Request.Headers["X-Language"].ToString();

        if (string.IsNullOrEmpty(lang))
        {
            lang = context.Request.Headers["Accept-Language"].ToString();
        }

        if (string.IsNullOrEmpty(lang)) return "en"; 
        if (lang.Length >= 2) lang = lang.Substring(0, 2);
        return lang.ToLower();
    }
}