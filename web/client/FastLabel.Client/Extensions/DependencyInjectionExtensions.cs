using FastLabel.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace FastLabel.Client.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        var serverAddress = configuration["ServerUrl"] ?? "http://localhost:6000";
        
        // 1. Composants Blazor
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // 2. Configuration des clients HTTP nommés
        services.AddTransient<CustomAuthorizationMessageHandler>();

        // Client public (pour login, register, etc.)
        services.AddHttpClient("PublicClient", client =>
        {
            client.BaseAddress = new Uri(serverAddress);
        });

        // Client sécurisé (pour les appels authentifiés)
        services.AddHttpClient("AuthClient", client =>
        {
            client.BaseAddress = new Uri(serverAddress);
        })
        .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();

        // 3. Services métiers
        services.AddScoped<AuthService>();
        services.AddScoped<TranslationService>();
        services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

        return services;
    }
}