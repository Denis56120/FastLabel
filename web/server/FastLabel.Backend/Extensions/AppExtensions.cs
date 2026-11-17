namespace FastLabel.Backend.Extensions;

public static class AppExtensions
{
    private static async Task<WebApplication> UseDatabaseMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();

            // En mode développement, si tu veux vider/recréer automatiquement :
            // await dbContext.Database.EnsureDeletedAsync();

            // Applique automatiquement toutes les migrations en attente
            await dbContext.Database.MigrateAsync();

            // Optionnel : Appeler un service de Seed ici si tu en as un
            // await AppDbContextSeed.SeedAsync(dbContext);
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Une erreur s'est produite lors de la migration de la base de données.");
        }

        return app;
    }

    public static async Task<WebApplication> ConfigurePipelineAsync(this WebApplication app)
    {
        if (!app.Environment.IsEnvironment("Testing"))
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var dbContext = services.GetRequiredService<AppDbContext>();
                var configuration = services.GetRequiredService<IConfiguration>();
                var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();

                var initializer = new DbInitializer(dbContext, configuration, passwordHasher);
                await initializer.InitializeAsync();

                if (app.Environment.IsDevelopment())
                {
                    var dataseeder = new DevelopmentDataSeeder(dbContext, passwordHasher);
                    await dataseeder.SeedAsync();   

                    app.UseDeveloperExceptionPage();         
                }
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<WebApplication>>();
                logger.LogError(ex, "Erreur critique lors de l'initialisation de la DB.");
                throw;
            }
        }

        await app.UseDatabaseMigrationsAsync();

        app.UseResponseCompression();

        app.UseCors("AllowBlazorClient");
        app.UseWebSockets();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapAuthEndpoints();
        app.MapPrinterEndpoints();
        app.MapTemplateEndpoints();
        app.MapAgentEndpoints();
        app.MapPrintJobEndpoints();
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
        app.MapResellerEndpoints();
        app.MapAntiforgeryEndpoints();

        return app;
    }
}