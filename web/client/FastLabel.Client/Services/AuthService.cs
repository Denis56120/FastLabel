using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Authorization;
using FastLabel.Shared.DTOs;

namespace FastLabel.Client.Services;

public class AuthService(
    IHttpClientFactory clientFactory,
    IJSRuntime jsRuntime,
    AuthenticationStateProvider authStateProvider,
    TokenContainer tokenContainer,
    CookieService cookieService) 
{
    private readonly HttpClient _httpClient = clientFactory.CreateClient("AuthClient");
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private readonly AuthenticationStateProvider _authProvider = authStateProvider;
    private readonly TokenContainer _tokenContainer = tokenContainer;
    private readonly CookieService _cookieService = cookieService;
    private string? _token;

    public async Task SetTokenAsync(string token)
    {
        _token = token;
        _tokenContainer.Token = token;
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
        return await _httpClient.GetFromJsonAsync<AuthResponseDto>("api/auth/me");
    }
}