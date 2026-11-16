namespace FastLabel.Client.Services;

public class CustomAuthenticationStateProvider(IHttpClientFactory clientFactory) : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient = clientFactory.CreateClient("AuthClient");
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/users/me");

            if (response.IsSuccessStatusCode)
            {
                var userInfo = await response.Content.ReadFromJsonAsync<UserDto>();

                if (userInfo != null && !string.IsNullOrEmpty(userInfo.Email))
                {
                    var role = userInfo.IsAdmin ? "Admin" : (userInfo.IsReseller ? "Reseller" : "User");
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, userInfo.Name ?? string.Empty),
                        new(ClaimTypes.Email, userInfo.Email ?? string.Empty),
                        new(ClaimTypes.Role, role)
                    };

                    var identity = new ClaimsIdentity(claims, "cookieAuth");
                    return new AuthenticationState(new ClaimsPrincipal(identity));
                }
            }
        }
        catch
        {
            // Ignorer l'erreur réseau ou 401 si non connecté
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