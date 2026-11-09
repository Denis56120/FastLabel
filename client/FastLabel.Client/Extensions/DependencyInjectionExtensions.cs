using FastLabel.Client.Services;

namespace FastLabel.Client.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        var baseAddress = configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
        
        // 1. Composants Blazor
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // 2. Configuration des clients HTTP (avec les clients nommés pour éviter les conflits)
        services.AddTransient<CustomAuthorizationMessageHandler>();

        // 3. Client public
        services.AddHttpClient("PublicClient", client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        });

        // 4. Client sécurisé (qui hérite de la même URL)
        services.AddHttpClient("AuthClient", client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        })
        .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();

        // Si tu veux que le HttpClient par défaut soit le sécurisé
        services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AuthClient"));

        // 3. Services métiers
        services.AddScoped<AuthService>();

        return services;
    }
}