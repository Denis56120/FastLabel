namespace FastLabel.Client.Services;

public class TranslationService(IHttpClientFactory httpClientFactory, CookieService cookieService)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly CookieService _cookieService = cookieService;
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = "en";
    private string? _loadedLanguage;
    private Task? _loadingTask;

    public event Action? OnChange;

    public string CurrentLanguage => _currentLanguage;

    public string this[string key] => GetString(key);

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
            else
            {
                var browserLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (!string.IsNullOrEmpty(browserLang))
                {
                    language = browserLang.Equals("fr", StringComparison.OrdinalIgnoreCase) ? "fr" : "en";
                }
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

            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<List<TranslationDto>>(jsonString, options);

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

    public async Task SetLanguageAsync(string language)
    {
        if (string.IsNullOrEmpty(language)) return;

        await _cookieService.SetCookieAsync("language", language);

        await InitializeAsync(language);
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