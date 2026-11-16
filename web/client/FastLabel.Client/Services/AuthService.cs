namespace FastLabel.Client.Services;

public class AuthService(IHttpClientFactory clientFactory, AuthenticationStateProvider authProvider)
{
    private readonly HttpClient _http = clientFactory.CreateClient("AuthClient");
    private readonly AuthenticationStateProvider _authProvider = authProvider;

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