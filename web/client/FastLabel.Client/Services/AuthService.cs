using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using FastLabel.Shared.DTOs; // <-- Importe le namespace partagé ici

public class AuthService
{
    private readonly HttpClient _http;
    private readonly AuthenticationStateProvider _authProvider;

    public AuthService(IHttpClientFactory clientFactory, AuthenticationStateProvider authProvider)
    {
        _http = clientFactory.CreateClient("AuthClient");
        _authProvider = authProvider;
    }

    public async Task<bool> LoginAsync(LoginDto model)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", model);
        if (response.IsSuccessStatusCode)
        {
            if (_authProvider is CustomAuthenticationStateProvider customProvider)
            {
                customProvider.NotifyUserAuthentication();
            }
            return true;
        }
        return false;
    }

    public async Task LogoutAsync()
    {
        await _http.PostAsync("api/auth/logout", null);

        if (_authProvider is CustomAuthenticationStateProvider customProvider)
        {
            customProvider.NotifyUserLogout();
        }
    }
}