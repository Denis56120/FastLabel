using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Loader;
using System.Text;
using FastLabel.Core;

namespace FastLabel.Service;

public class ServiceHostWorker(ILogger<ServiceHostWorker> logger) : BackgroundService
{
    private readonly ILogger<ServiceHostWorker> _logger = logger;
    private IFastLabelPlugin? _activePlugin;
    private AssemblyLoadContext? _loadContext;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckAndUpdateAndRunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Arrêt du service demandé par Windows...");

        // 1. Stopper le Tray proprement
        StopTrayApplication();

        // 2. Décharger la DLL du plugin
        UnloadPlugin();

        // Appel de la méthode parente pour arrêter proprement le worker
        await base.StopAsync(cancellationToken);
    }

    private async Task CheckAndUpdateAndRunAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Vérification des mises à jour...");

        // 1. Vérifier si une MAJ est disponible (via ton API Cloud)
        bool updateAvailable = await CheckForUpdatesAsync();

        if (updateAvailable)
        {
            _logger.LogInformation("Mise à jour détectée. Application du processus de MAJ...");

            // 2. Télécharger les nouveaux binaires dans un dossier temporaire
            await DownloadUpdatesAsync();

            // 3. Stopper proprement le Tray existant (pour libérer le fichier .exe)
            StopTrayApplication();

            // 4 Décharger l'ancien plugin (pour libérer la DLL)
            UnloadPlugin();

            // 5. Copier les nouveaux fichiers par-dessus les anciens
            ApplyDownloadedFiles();
        }

        // 6. Charger et lancer le plugin (version actuelle ou fraîchement mise à jour)
        LoadAndLaunchPlugin();

        // 7. Démarrer ou redémarrer le Tray dans la session utilisateur
        LaunchTrayApplication();
    }

    private async Task<bool> CheckForUpdatesAsync()
    {
        // TODO: Interroger ton endpoint de versioning (ex: comparer la version locale avec le cloud)
        await Task.Delay(100);
        return false; // Mettre à true quand tu implémenteras le serveur de màj
    }

    private async Task DownloadUpdatesAsync()
    {
        // TODO: Télécharger les nouveaux fichiers zip/binaires dans un dossier temp
        await Task.CompletedTask;
    }

    private void StopTrayApplication()
    {
        try
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("FastLabel.Tray"))
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(3000))
                {
                    process.Kill(); // Force l'arrêt si le Tray ne répond pas
                }
            }
            _logger.LogInformation("Application Tray stoppée.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'arrêter proprement l'application Tray.");
        }
    }

    private void ApplyDownloadedFiles()
    {
        // TODO: Copier les fichiers du dossier temporaire vers AppDomain.CurrentDomain.BaseDirectory
        _logger.LogInformation("Fichiers mis à jour appliqués.");
    }

    private void LaunchTrayApplication()
    {
        try
        {
            string trayPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FastLabel.Tray.exe");

            if (!File.Exists(trayPath))
            {
                _logger.LogWarning("FastLabel.Tray.exe introuvable à l'emplacement : {Path}", trayPath);
                return;
            }

            // Vérifier si la Tray tourne déjà
            if (Process.GetProcessesByName("FastLabel.Tray").Length > 0)
            {
                _logger.LogInformation("La Tray est déjà active.");
                return;
            }

            // Lancement dans la session utilisateur active depuis le Service
            _logger.LogInformation("Lancement de l'interface Tray pour l'utilisateur...");
            ProcessLauncher.StartProcessInUserSession(trayPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossible de lancer l'application Tray.");
        }
    }

    private void LoadAndLaunchPlugin()
    {
        string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FastLabel.Service.Plugin.dll");

        if (!File.Exists(pluginPath))
        {
            _logger.LogWarning("Plugin introuvable à l'emplacement : {Path}. En attente du premier déploiement...", pluginPath);
            return;
        }

        // Création d'un contexte isolé et collectable (permet le remplacement à chaud sans redémarrer le service)
        _loadContext = new AssemblyLoadContext("FastLabelPluginContext", isCollectible: true);

        // Lecture sous forme de FileStream pour ne pas verrouiller le fichier DLL sur le disque
        using var fs = new FileStream(pluginPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var assembly = _loadContext.LoadFromStream(fs);

        // Recherche de la classe qui implémente IFastLabelPlugin
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IFastLabelPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract) ?? throw new InvalidOperationException("Aucune classe valide implémentant IFastLabelPlugin n'a été trouvée dans la DLL.");

        // Instanciation dynamique et appel de Launch()
        _activePlugin = (IFastLabelPlugin?)Activator.CreateInstance(pluginType);
        _activePlugin?.Launch(Environment.GetCommandLineArgs());

        _logger.LogInformation("Plugin métier chargé et méthode Launch() exécutée avec succès.");
    }

    private void UnloadPlugin()
    {
        try
        {
            _activePlugin?.Stop();
            _activePlugin = null;

            _loadContext?.Unload();
            _loadContext = null;

            _logger.LogInformation("Plugin métier déchargé proprement.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du déchargement du plugin.");
        }
    }

    private void StartPipeListener(CancellationToken stoppingToken)
    {
        Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream("FastLabelUpdatePipe", PipeDirection.In);
                    await server.WaitForConnectionAsync(stoppingToken);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string command = await reader.ReadToEndAsync();

                    if (command.Trim() == "TRIGGER_UPDATE")
                    {
                        _logger.LogInformation("Ordre de mise à jour reçu depuis le Tray.");
                        // Déclencher ton flux de MAJ (téléchargement, arrêt Tray, déchargement plugin, copie...)
                        await CheckAndUpdateAndRunAsync(stoppingToken);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Erreur dans le serveur NamedPipe du service.");
                }
            }
        }, stoppingToken);
    }
}