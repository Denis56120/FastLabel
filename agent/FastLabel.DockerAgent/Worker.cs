using FastLabel.Service.Plugin;

namespace FastLabel.DockerAgent;

public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    private PluginEntryPoint? _plugin;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Récupération des paramètres depuis les variables d'environnement (ou appsettings.json)
        string baseUrl = configuration["CloudUrl"] ?? "fastlabel.com";
        string apiKey = configuration["ApiKey"] ?? string.Empty;

        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("ATTENTION : Aucune ApiKey n'est configurée pour l'agent Docker ! Pensez à définir la variable d'environnement ApiKey.");
        }

        logger.LogInformation("Démarrage de l'agent Docker FastLabel vers {BaseUrl}...", baseUrl);

        _plugin = new PluginEntryPoint();
        
        // Lancement de la boucle WebSocket du plugin
        _plugin.Launch(baseUrl);

        try
        {
            // Maintien du worker en vie tant que l'arrêt n'est pas demandé
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Arrêt propre géré par le token
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Arrêt de l'agent Docker FastLabel en cours...");
        _plugin?.Stop();
        return base.StopAsync(cancellationToken);
    }
}