using Microsoft.AspNetCore.Components.WebAssembly.Http;

public class CustomAuthorizationMessageHandler : DelegatingHandler
{
    private readonly TranslationService _translationService;

    public CustomAuthorizationMessageHandler(TranslationService translationService)
    {
        _translationService = translationService;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        var currentLang = _translationService?.CurrentLanguage ?? "en";
        request.Headers.Remove("X-Language");
        request.Headers.Add("X-Language", currentLang);

        return base.SendAsync(request, cancellationToken);
    }
}