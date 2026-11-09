using System.Net.Http.Headers;
using FastLabel.Client.Services;

public class CustomAuthorizationMessageHandler : DelegatingHandler
{
    private readonly TokenContainer _tokenContainer;

    public CustomAuthorizationMessageHandler(TokenContainer tokenContainer)
    {
        _tokenContainer = tokenContainer;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_tokenContainer.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenContainer.Token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}