using Microsoft.JSInterop;

namespace FastLabel.Client.Services;

public class CookieService(IJSRuntime jsRuntime)
{
    private readonly IJSRuntime _jsRuntime = jsRuntime;

    public async Task<string?> GetCookieAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("appInterop.getCookie", key);
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
            await _jsRuntime.InvokeVoidAsync("appInterop.setCookie", key, value, days);
        }
        catch
        {
        }
    }
}