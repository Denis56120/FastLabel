using FastLabel.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

public class CustomAuthorizationMessageHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;

    public CustomAuthorizationMessageHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Résolution dynamique pour éviter la dépendance circulaire au démarrage
        using var scope = _serviceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        var token = await authService.GetTokenAsync(); // Récupérez votre token ici

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}