using System.Net.Http.Headers;

namespace FastLabel.Client.Services;

public class CustomAuthorizationMessageHandler : DelegatingHandler
{
    private readonly AuthService _authService;

    public CustomAuthorizationMessageHandler(AuthService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authService.GetTokenAsync();
        
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}