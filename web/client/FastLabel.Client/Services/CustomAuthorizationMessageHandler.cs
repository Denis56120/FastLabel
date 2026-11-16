namespace FastLabel.Client.Services;

public class CustomAuthorizationMessageHandler(TranslationService translationService) : DelegatingHandler
{
    private readonly TranslationService _translationService = translationService;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        return base.SendAsync(request, cancellationToken);
    }
}