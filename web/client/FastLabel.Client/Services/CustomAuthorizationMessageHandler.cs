using System.Net.Http.Headers;

public class CustomAuthorizationMessageHandler : DelegatingHandler
{
    private readonly TokenContainer _tokenContainer;
    private readonly TranslationService _translationService;

    public CustomAuthorizationMessageHandler(TokenContainer tokenContainer, TranslationService translationService)
    {
        _tokenContainer = tokenContainer;
        _translationService = translationService;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_tokenContainer.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenContainer.Token);
        }

        var currentLang = _translationService?.CurrentLanguage ?? "en";
        request.Headers.Remove("X-Language");
        request.Headers.Add("X-Language", currentLang);

        Console.WriteLine($"X-Language : {currentLang}");

        return base.SendAsync(request, cancellationToken);
    }
}