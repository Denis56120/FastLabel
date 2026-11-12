using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace FastLabel.Client.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());
    private bool _initialized;
    private AuthenticationState _cachedState;

    public CustomAuthenticationStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        _cachedState = new AuthenticationState(_anonymous);
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_initialized)
        {
            return _cachedState;
        }

        string? token = null;
        try
        {
            token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
        }
        catch
        {
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwtAuthType");
            var user = new ClaimsPrincipal(identity);
            _cachedState = new AuthenticationState(user);
        }

        _initialized = true;
        return _cachedState;
    }

    public void NotifyUserAuthentication(string token)
    {
        var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(ParseClaimsFromJwt(token), "jwtAuthType"));
        _cachedState = new AuthenticationState(authenticatedUser);
        _initialized = true;
        NotifyAuthenticationStateChanged(Task.FromResult(_cachedState));
    }

    public void NotifyUserLogout()
    {
        _cachedState = new AuthenticationState(_anonymous);
        _initialized = true;
        NotifyAuthenticationStateChanged(Task.FromResult(_cachedState));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        var claims = new List<Claim>();
        if (keyValuePairs == null) return claims;

        foreach (var kvp in keyValuePairs)
        {
            var claimType = kvp.Key;
            if (claimType == "role" || claimType.EndsWith("/identity/claims/role"))
            {
                claimType = ClaimTypes.Role;
            }

            if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    claims.Add(new Claim(claimType, item.ToString()!));
                }
            }
            else if (kvp.Value is JsonElement singleElement)
            {
                claims.Add(new Claim(claimType, singleElement.ToString()!));
            }
            else
            {
                claims.Add(new Claim(claimType, kvp.Value.ToString()!));
            }
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}