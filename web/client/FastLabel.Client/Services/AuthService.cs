using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Authorization;
using FastLabel.Shared.DTOs;
using System.Net.Http.Json;

namespace FastLabel.Client.Services;

public class AuthService(
    IHttpClientFactory clientFactory,
    IServiceProvider serviceProvider,
    AuthenticationStateProvider authStateProvider,
    TokenContainer tokenContainer) 
{
    private readonly IHttpClientFactory _clientFactory = clientFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly AuthenticationStateProvider _authProvider = authStateProvider;
    private readonly TokenContainer _tokenContainer = tokenContainer;
    
    private IJSRuntime? _jsRuntime;
    private IJSRuntime JsRuntime => _jsRuntime ??= _serviceProvider.GetRequiredService<IJSRuntime>();

    private HttpClient Http => _clientFactory.CreateClient("AuthClient");
    private string? _token;

    public async Task SetTokenAsync(string token)
    {
        _token = token;
        _tokenContainer.Token = token;
        await JsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);

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
                _token = await JsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
                _tokenContainer.Token = _token;
            }
            catch
            {
            }
        }
        return _token;
    }

    public async Task LogoutAsync()
    {
        _token = null;
        _tokenContainer.Token = null;
        await JsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");

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
        return await Http.GetFromJsonAsync<AuthResponseDto>("api/auth/me");
    }
}