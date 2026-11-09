using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace FastLabel.Client.Services;

public class CookieService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IHttpContextAccessor _httpContextAccessor;


    public CookieService(IJSRuntime jsRuntime, IHttpContextAccessor httpContextAccessor)
    {
        _jsRuntime = jsRuntime;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string?> GetCookieAsync(string key)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            // 🔍 Ajoute ce log pour voir ce que le serveur reçoit réellement en SSR
            var cookiesList = string.Join(", ", httpContext.Request.Cookies.Select(c => $"{c.Key}={c.Value}"));
            Console.WriteLine($"[SSR COOKIE DEBUG] Clés cherchée: {key} | Cookies reçus par le serveur: [{cookiesList}]");

            if (httpContext.Request.Cookies.TryGetValue(key, out var cookieValue))
            {
                return cookieValue;
            }
            return null;
        }

        try
        {
            var val = await _jsRuntime.InvokeAsync<string?>("appInterop.getCookie", key);
            Console.WriteLine($"[CLIENT COOKIE DEBUG] Clé: {key} | Valeur JS: {val}");
            return val;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetCookieAsync(string key, string value, int days = 30)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null && !httpContext.Response.HasStarted)
        {
            httpContext.Response.Cookies.Append(key, value, new CookieOptions 
            { 
                Expires = DateTimeOffset.UtcNow.AddDays(days), 
                Path = "/",
                IsEssential = true
            });
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("appInterop.setCookie", key, value, days);
        }
        catch
        {
            // Ignorer si le JS n'est pas prêt
        }
    }
}