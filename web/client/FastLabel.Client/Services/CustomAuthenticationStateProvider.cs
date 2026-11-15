namespace FastLabel.Client.Services;

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
            // Appel vers le backend pour récupérer l'utilisateur connecté via le cookie HttpOnly
            var response = await _httpClient.GetAsync("api/auth/me");

            if (response.IsSuccessStatusCode)
            {
                var userInfo = await response.Content.ReadFromJsonAsync<UserDto>();

                if (userInfo != null && !string.IsNullOrEmpty(userInfo.Email))
                {
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, userInfo.Name ?? string.Empty),
                        new(ClaimTypes.Email, userInfo.Email ?? string.Empty),
                        new(ClaimTypes.Role, userInfo.IsAdmin ? "Admin" : "User")
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