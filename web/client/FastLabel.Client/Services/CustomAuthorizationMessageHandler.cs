namespace FastLabel.Client.Services;

public class CustomAuthorizationMessageHandler(CookieService cookieService) : DelegatingHandler
{
    private readonly CookieService _cookieService = cookieService;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        if (request.Method == HttpMethod.Post || 
            request.Method == HttpMethod.Put || 
            request.Method == HttpMethod.Delete ||
            request.Method == HttpMethod.Patch)
        {
            var xsrfToken = await _cookieService.GetCookieAsync("XSRF-TOKEN");

            if (!string.IsNullOrEmpty(xsrfToken))
            {
                var decodedToken = Uri.UnescapeDataString(xsrfToken);
                
                if (!request.Headers.Contains("X-XSRF-TOKEN"))
                {
                    request.Headers.Add("X-XSRF-TOKEN", decodedToken);
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}