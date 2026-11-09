using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Authorization;
using FastLabel.Shared.DTOs;

namespace FastLabel.Client.Services;

public class AuthService(IHttpClientFactory clientFactory, IJSRuntime jsRuntime, AuthenticationStateProvider authStateProvider)
{
    private readonly HttpClient _httpClient = clientFactory.CreateClient("AuthClient");
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private readonly AuthenticationStateProvider _authProvider = authStateProvider;
    private string? _token;

    public async Task SetTokenAsync(string token)
    {
        _token = token;
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);

        if (_authProvider is CustomAuthenticationStateProvider customProvider)
        {
            customProvider.NotifyUserAuthentication(token);
        }
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

        if (_authProvider is CustomAuthenticationStateProvider customProvider)
        {
            customProvider.NotifyUserLogout();
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public async Task<AuthResponseDto?> GetCurrentUserAsync()
    {
        var token = await GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await _httpClient.GetFromJsonAsync<AuthResponseDto>("api/auth/me");
    }
}