namespace FastLabel.Backend.Extensions;

public static class AppExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        
        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var configuration = services.GetRequiredService<IConfiguration>();
            var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();
            
            DbInitializer.InitializeAsync(dbContext, configuration, passwordHasher).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<WebApplication>>();
            logger.LogError(ex, "Erreur critique lors de l'initialisation de la DB.");
            throw; 
        }

        app.UseResponseCompression();

        app.UseCors("AllowBlazorClient");
        app.UseWebSockets();

        app.UseAuthentication();
        app.UseAuthorization();

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
        app.MapAgentTypeEndpoints();
        app.MapProtocolEndpoints();
        app.MapResolutionDpiEndpoints();
        app.MapSubscriptionEndpoints();
        app.MapStripeEndpoints();

        return app;
    }
}