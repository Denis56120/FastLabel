using System.Net.Http.Json;
using FastLabel.Client.Services;
using FastLabel.Shared.DTOs;

public class TranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CookieService _cookieService;
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = "fr-FR";
    private string? _loadedLanguage;
    private Task? _loadingTask;

    public event Action? OnChange;

    public string CurrentLanguage => _currentLanguage;

    public string this[string key] => GetString(key);

    public TranslationService(IHttpClientFactory httpClientFactory, CookieService cookieService)
    {
        _httpClientFactory = httpClientFactory;
        _cookieService = cookieService;
    }

    /// <summary>
    /// Initialise le service en lisant automatiquement le cookie de langue s'il existe.
    /// </summary>
    public async Task InitializeAsync()
    {
        string language = _currentLanguage;

        try
        {
            var cookieValue = await _cookieService.GetCookieAsync("language");
            if (!string.IsNullOrEmpty(cookieValue))
            {
                language = cookieValue;
            }
        }
        catch
        {
            // En cas d'erreur ou pendant le Prerendering, on conserve la langue par défaut
        }

        await InitializeAsync(language);
    }

    /// <summary>
    /// Charge les traductions pour une langue donnée (avec dédoublonnage des requêtes).
    /// </summary>
    public async Task InitializeAsync(string language)
    {
        var targetLanguage = string.IsNullOrEmpty(language) ? _currentLanguage : language;

        // 1. Si la langue demandée est déjà en mémoire et chargée, inutile de refaire la requête HTTP
        if (_loadedLanguage == targetLanguage && _translations.Count > 0)
        {
            return;
        }

        _currentLanguage = targetLanguage;

        // 2. Si un chargement est DÉJÀ en cours (ex: 2 composants appellent Initialize simultanément),
        // on attend la fin du chargement existant au lieu d'en lancer un deuxième.
        if (_loadingTask != null && !_loadingTask.IsCompleted)
        {
            await _loadingTask;
            return;
        }

        // 3. Exécution du chargement avec verrou
        _loadingTask = FetchTranslationsAsync(_currentLanguage);
        await _loadingTask;
    }

    private async Task FetchTranslationsAsync(string language)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PublicClient");
            var url = $"api/translations/{language}";

            var result = await client.GetFromJsonAsync<List<TranslationDto>>(url);

            if (result != null)
            {
                _translations = result.ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
                _loadedLanguage = language;
            }
            else
            {
                _translations = [];
                _loadedLanguage = null;
            }
        }
        catch
        {
            _translations = [];
            _loadedLanguage = null;
        }

        // Déclenche l'événement pour avertir l'UI uniquement une fois les traductions appliquées
        OnChange?.Invoke();
    }

    public string GetString(string key)
    {
        if (_translations.TryGetValue(key, out var value))
        {
            return value;
        }
        return key;
    }
}