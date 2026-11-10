using System.Globalization;
using Microsoft.AspNetCore.Localization;
using FastLabel.Client.Components;

namespace FastLabel.Client.Extensions;

public static class AppPipelineExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

        // Configuration de la localisation par cookie ("language")
        var supportedLanguages = new[] { "fr-FR", "en_US" }; 
        var localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture("fr-FR")
            .AddSupportedCultures(supportedLanguages)
            .AddSupportedUICultures(supportedLanguages);

        var cookieProvider = localizationOptions.RequestCultureProviders
            .OfType<CookieRequestCultureProvider>()
            .First();
        cookieProvider.CookieName = "language";

        app.UseRequestLocalization(localizationOptions);

        // AJOUT : Indispensable pour que le cookie d'authentification soit lu et validé
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}