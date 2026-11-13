using Microsoft.JSInterop;

namespace FastLabel.Client.Services;

public class CookieService(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private IJSRuntime? _jsRuntime;

    private IJSRuntime JsRuntime => 
        _jsRuntime ??= _serviceProvider.GetRequiredService<IJSRuntime>();

    public async Task<string?> GetCookieAsync(string key)
    {
        try
        {
            return await JsRuntime.InvokeAsync<string?>("appInterop.getCookie", key);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetCookieAsync(string key, string value, int days = 30)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("appInterop.setCookie", key, value, days);
        }
        catch
        {
        }
    }
}