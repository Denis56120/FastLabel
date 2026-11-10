using System.Net.Http.Json;
using FastLabel.Client.Services;
using FastLabel.Shared.DTOs;
// Assure-toi d'importer le namespace de ton service de cookies (ex: FastLabel.Client.Services)

public class TranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CookieService _cookieService; // Injection de ton CookieService
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = "fr-FR";

    public event Action? OnChange;

    public string CurrentLanguage => _currentLanguage;

    public string this[string key] => GetString(key);

    public TranslationService(IHttpClientFactory httpClientFactory, CookieService cookieService)
    {
        _httpClientFactory = httpClientFactory;
        _cookieService = cookieService;
    }

    // Nouvelle méthode sans paramètre qui lit le cookie automatiquement
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
            // Ignore pendant le prerendering, on garde la langue actuelle
        }

        await InitializeAsync(language);
    }

    public async Task InitializeAsync(string language)
    {
        var targetLanguage = string.IsNullOrEmpty(language) ? _currentLanguage : language;
        _currentLanguage = targetLanguage;

        try
        {
            var client = _httpClientFactory.CreateClient("PublicClient");
            var url = $"api/translations/{_currentLanguage}";
            
            var result = await client.GetFromJsonAsync<List<TranslationDto>>(url);
            
            if (result != null)
            {
                _translations = result.ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _translations = [];
            }
        }
        catch
        {
            _translations = [];
        }

        // Déclenche l'événement pour forcer la mise à jour des layouts et du routeur
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