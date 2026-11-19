namespace FastLabel.Client.Services;

public class CustomAuthenticationStateProvider(IHttpClientFactory clientFactory) : AuthenticationStateProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient = clientFactory.CreateClient("AuthClient");
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private AuthenticationState? _cachedState;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
    private Task<AuthenticationState>? _inflightTask;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return GetAuthenticationStateAsync(forceRefresh: false);
    }

    public void NotifyUserAuthentication()
    {
        _cachedState = null;
        _cachedAt = DateTimeOffset.MinValue;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync(forceRefresh: true));
    }

    public void NotifyUserLogout()
    {
        _cachedState = new AuthenticationState(_anonymous);
        _cachedAt = DateTimeOffset.UtcNow;
        NotifyAuthenticationStateChanged(Task.FromResult(_cachedState));
    }

    private Task<AuthenticationState> GetAuthenticationStateAsync(bool forceRefresh)
    {
        var now = DateTimeOffset.UtcNow;

        if (!forceRefresh && _cachedState is not null && now - _cachedAt < CacheDuration)
        {
            return Task.FromResult(_cachedState);
        }

        _inflightTask ??= FetchAndCacheStateAsync();
        return _inflightTask;
    }

    private async Task<AuthenticationState> FetchAndCacheStateAsync()
    {
        await _syncLock.WaitAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_cachedState is not null && now - _cachedAt < CacheDuration)
            {
                return _cachedState;
            }

            var state = await LoadAuthenticationStateAsync();
            _cachedState = state;
            _cachedAt = DateTimeOffset.UtcNow;
            return state;
        }
        finally
        {
            _inflightTask = null;
            _syncLock.Release();
        }
    }

    private async Task<AuthenticationState> LoadAuthenticationStateAsync()
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
            if (_cachedState is not null)
            {
                return _cachedState;
            }
        }

        return new AuthenticationState(_anonymous);
    }
}