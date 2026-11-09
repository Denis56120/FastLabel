using FastLabel.Client.Components;

namespace FastLabel.Client.Extensions;

public static class AppPipelineExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Gestion des erreurs en production
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAntiforgery();

        // Routing et Composants
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}