using Microsoft.JSInterop;
using FastLabel.Shared.DTOs;

namespace FastLabel.Client.Services;

public class AuthService(IHttpClientFactory clientFactory, IJSRuntime jsRuntime)
{
    private readonly HttpClient _httpClient = clientFactory.CreateClient("AuthClient");
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private string? _token;

    public async Task SetTokenAsync(string token)
    {
        _token = token;
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
    }

    public async Task<string?> GetTokenAsync()
    {
        if (string.IsNullOrEmpty(_token))
        {
            try
            {
                _token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
            }
            catch
            {
                // Ignorer si le JS n'est pas encore disponible (rendu prerender)
            }
        }
        return _token;
    }

    public async Task LogoutAsync()
    {
        _token = null;
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public async Task<AuthResponseDto?> GetCurrentUserAsync()
    {
        // Optionnel : s'assurer que le token est injecté dans les en-têtes si l'API est sécurisée par Bearer
        var token = await GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await _httpClient.GetFromJsonAsync<AuthResponseDto>("api/auth/me");
    }
}