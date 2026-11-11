using System.Net.Http.Json;
using FastLabel.Client.Services;
using FastLabel.Shared.DTOs;

public class TranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CookieService _cookieService;
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = "en";
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
        }

        await InitializeAsync(language);
    }

    public async Task InitializeAsync(string language)
    {
        var targetLanguage = string.IsNullOrEmpty(language) ? _currentLanguage : language;

        if (_loadedLanguage == targetLanguage && _translations.Count > 0)
        {
            return;
        }

        _currentLanguage = targetLanguage;

        if (_loadingTask != null && !_loadingTask.IsCompleted)
        {
            await _loadingTask;
            return;
        }

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