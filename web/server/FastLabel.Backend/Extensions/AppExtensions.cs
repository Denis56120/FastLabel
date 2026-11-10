using FastLabel.Backend.Data;
using FastLabel.Backend.Endpoints;
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
            DbInitializer.InitializeAsync(dbContext).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Une erreur est survenue lors de l'initialisation de la base de données.");
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