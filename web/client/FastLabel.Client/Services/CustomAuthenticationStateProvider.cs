using System.Security.Claims;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using FastLabel.Shared.DTOs; 

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public CustomAuthenticationStateProvider(IHttpClientFactory clientFactory)
    {
        _httpClient = clientFactory.CreateClient("AuthClient");
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var userInfo = await _httpClient.GetFromJsonAsync<UserDto>("api/auth/me");

            if (userInfo != null && !string.IsNullOrEmpty(userInfo.Email))
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, userInfo.Name),
                    new(ClaimTypes.Email, userInfo.Email),
                    new(ClaimTypes.Role, userInfo.IsAdmin ? "Admin" : "Standard")
                };

                var identity = new ClaimsIdentity(claims, "cookieAuth");
                return new AuthenticationState(new ClaimsPrincipal(identity));
            }
        }
        catch
        {
            
        }

        return new AuthenticationState(_anonymous);
    }

    public void NotifyUserAuthentication()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void NotifyUserLogout()
    {
        var authState = Task.FromResult(new AuthenticationState(_anonymous));
        NotifyAuthenticationStateChanged(authState);
    }
}