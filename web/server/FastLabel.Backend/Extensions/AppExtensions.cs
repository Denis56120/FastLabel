using FastLabel.Backend.Data;
using FastLabel.Backend.Endpoints;
using FastLabel.Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Extensions;

public static class AppExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Initialisation de la BDD au démarrage
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        
        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();
            
            DbInitializer.InitializeAsync(dbContext, passwordHasher).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Optionnel : un petit log en cas d'échec pour t'aider à débugger
            var logger = services.GetRequiredService<ILogger<WebApplication>>();
            logger.LogError(ex, "An error occurred while initializing the database.");
        }

        // Ordre des Middlewares
        app.UseCors("AllowBlazorClient");
        app.UseWebSockets();

        app.UseAuthentication();
        app.UseAuthorization();

        // Mapping des endpoints modulaires
        app.MapPrinterEndpoints();
        app.MapTemplateEndpoints();
        app.MapAgentEndpoints();
        app.MapPrintJobEndpoints();
        app.MapAuthEndpoints();
        app.MapAgentWebSocketEndpoints();
        app.MapPrintEndpoints();
        app.MapUserEndpoints();
        app.MapTranslationEndpoints();
        app.MapLanguageEndpoints();

        return app;
    }
}