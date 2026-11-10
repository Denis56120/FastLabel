using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using FastLabel.Core;

namespace FastLabel.Service;

public class ServiceHostWorker() : BackgroundService
{
    private IFastLabelPlugin? _activePlugin;
    private AssemblyLoadContext? _loadContext;

    private readonly string baseUrl = "fastlabel.com";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Lancement de l'écouteur de pipe en arrière-plan
        _ = StartPipeListener(stoppingToken);

        await CheckAndUpdateAndRunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // 1. Stopper le Tray proprement
        StopTrayApplication();

        // 2. Décharger la DLL du plugin
        UnloadPlugin();

        // Appel de la méthode parente pour arrêter proprement le worker
        await base.StopAsync(cancellationToken);
    }

    private async Task CheckAndUpdateAndRunAsync(CancellationToken stoppingToken)
    {
        // 1. Vérifier si une MAJ est disponible
        string? zipUrl = await CheckForUpdatesAsync(stoppingToken);
        if (!string.IsNullOrEmpty(zipUrl))
        {
            // 2. Télécharger les nouveaux fichiers dans un dossier temporaire
            await DownloadUpdatesAsync(zipUrl, stoppingToken);

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

    private async Task<string> CheckVersionOnlyAsync(CancellationToken stoppingToken)
    {
        try
        {
            string apiUrl = $"https://{baseUrl}/api/version";

            using var httpClient = new HttpClient();

            // Timeout de sécurité pour éviter de bloquer le service si l'API est injoignable
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            HttpResponseMessage response = await httpClient.GetAsync(apiUrl, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                return "ErreurServeur";
            }

            string jsonResponse = await response.Content.ReadAsStringAsync(stoppingToken);

            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("version", out var versionElement) && versionElement.GetString() is string remoteVersion)
            {
                return remoteVersion;
            }

            return "FormatInconnu";
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Retourne un statut d'erreur lisible si l'API est injoignable (réseau coupé, etc.)
            return "HorsLigne";
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task<string> CheckStatusAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(100, stoppingToken);

        try
        {
            using var sc = new ServiceController("FastLabelService");

            return sc.Status switch
            {
                ServiceControllerStatus.Running => "Service en cours",
                ServiceControllerStatus.Stopped => "Service arrêté",
                ServiceControllerStatus.Paused => "Service en pause",
                ServiceControllerStatus.StartPending => "Démarrage en cours...",
                ServiceControllerStatus.StopPending => "Arrêt en cours...",
                _ => sc.Status.ToString()
            };
        }
        catch (Exception)
        {
            return "Statut inconnu";
        }
    }

    private async Task<string?> CheckForUpdatesAsync(CancellationToken stoppingToken)
    {
        // 1. Récupérer la version locale du plugin
        string localVersion = GetPluginDllVersion();

        // 2. Récupérer la version distante du plugin
        string remoteVersion = await CheckVersionOnlyAsync(stoppingToken);

        // 3. Comparer les versions
        if (Version.TryParse(localVersion, out var localVer) &&
            Version.TryParse(remoteVersion, out var remoteVer))
        {
            if (remoteVer > localVer)
            {                
                // Une nouvelle version est disponible, on retourne l'URL de téléchargement du ZIP
                return $"https://{baseUrl}/releases/FastLabel-{remoteVersion}.zip";
            }
        }

        // Aucune mise à jour nécessaire
        return null;
    }

    private static string GetPluginDllVersion()
    {
        try
        {
            // Chemin vers la DLL (ici dans le même dossier de base que l'application)
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fastlabel.Service.Plugin.dll");

            if (File.Exists(dllPath))
            {
                var assemblyName = AssemblyName.GetAssemblyName(dllPath);
                return assemblyName.Version?.ToString() ?? "Version inconnue";
            }
        }
        catch (Exception)
        {
        }

        return "1.0.0"; // Valeur par défaut de repli
    }

    private async Task DownloadUpdatesAsync(string zipUrl, CancellationToken stoppingToken)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FastLabelUpdate");
        string zipPath = Path.Combine(tempDir, "FastLabel.zip");
        string extractDir = Path.Combine(tempDir, "extracted");

        // 1. Nettoyer et préparer le dossier temporaire
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
        Directory.CreateDirectory(tempDir);

        // 2. Téléchargement du fichier ZIP
        using (var httpClient = new HttpClient())
        {
            using var response = await httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, stoppingToken);
            response.EnsureSuccessStatusCode();

            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, stoppingToken);
        }

        // 3. Le Dézippage (Extraction)
        Directory.CreateDirectory(extractDir);

        // C'est ici que la magie de la décompression opère :
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
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
                    process.Kill();
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private void ApplyDownloadedFiles()
    {
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FastLabelUpdate");
            string extractDir = Path.Combine(tempDir, "extracted");
            string installDir = AppDomain.CurrentDomain.BaseDirectory;

            if (!Directory.Exists(extractDir))
            {
                return;
            }

            // Copie récursive de tous les fichiers et dossiers
            foreach (string newPath in Directory.GetFiles(extractDir, "*.*", SearchOption.AllDirectories))
            {
                // Calcule le chemin relatif pour conserver l'arborescence (sous-dossiers, etc.)
                string relativePath = Path.GetRelativePath(extractDir, newPath);
                string targetPath = Path.Combine(installDir, relativePath);

                // S'assure que le sous-dossier de destination existe
                string? targetSubDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetSubDir) && !Directory.Exists(targetSubDir))
                {
                    Directory.CreateDirectory(targetSubDir);
                }

                // Copie et remplace le fichier existant
                File.Copy(newPath, targetPath, overwrite: true);
            }
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            // Nettoyage optionnel du dossier temporaire après application
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "FastLabelUpdate");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
                // Ignore les erreurs de nettoyage si un fichier est encore verrouillé
            }
        }
    }

    private void LaunchTrayApplication()
    {
        try
        {
            string trayPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FastLabel.Tray.exe");

            if (!File.Exists(trayPath))
            {
                return;
            }

            // Vérifier si la Tray tourne déjà
            if (Process.GetProcessesByName("FastLabel.Tray").Length > 0)
            {
                return;
            }

            // Lancement dans la session utilisateur active depuis le Service
            ProcessLauncher.StartProcessInUserSession(trayPath);
        }
        catch (Exception)
        {
        }
    }

    private void LoadAndLaunchPlugin()
    {
        string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FastLabel.Service.Plugin.dll");

        if (!File.Exists(pluginPath))
        {
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
        _activePlugin?.Launch(baseUrl);
    }

    private void UnloadPlugin()
    {
        try
        {
            _activePlugin?.Stop();
            _activePlugin = null;

            _loadContext?.Unload();
            _loadContext = null;
        }
        catch (Exception)
        {
        }
    }

    private async Task StartPipeListener(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream("FastLabelUpdatePipe", PipeDirection.InOut);
                await server.WaitForConnectionAsync(stoppingToken);

                using var reader = new StreamReader(server, Encoding.UTF8);
                string? command = await reader.ReadLineAsync(stoppingToken);

                if (!string.IsNullOrEmpty(command))
                {
                    string responseMessage = string.Empty;

                    switch (command.Trim())
                    {
                        case "CHECK_VERSION":
                            responseMessage = await CheckVersionOnlyAsync(stoppingToken);
                            break;

                        case "CHECK_STATUS":
                            if (OperatingSystem.IsWindows())
                            {
                                responseMessage = await CheckStatusAsync(stoppingToken);
                            }
                            else
                            {
                                responseMessage = "Non supporté";
                            }
                            break;

                        case "TRIGGER_UPDATE":
                            await CheckAndUpdateAndRunAsync(stoppingToken);
                            responseMessage = "UPDATE_TRIGGERED";
                            break;

                        default:
                            responseMessage = "UNKNOWN_COMMAND";
                            break;
                    }

                    // 2. On renvoie la réponse au Tray via le même flux (avec leaveOpen: true pour garder le pipe actif le temps d'écrire)
                    using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true);
                    await writer.WriteLineAsync(responseMessage);
                    await writer.FlushAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}