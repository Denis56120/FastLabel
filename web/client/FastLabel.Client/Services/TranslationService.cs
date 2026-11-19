namespace FastLabel.Client.Services;

public class TranslationService(IHttpClientFactory httpClientFactory, CookieService cookieService, IServiceProvider serviceProvider)
{
    public const string DefaultLanguage = "en";
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly CookieService _cookieService = cookieService;
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = DefaultLanguage;
    private string? _loadedLanguage;
    private Task? _loadingTask;
    private IJSRuntime? _jsRuntime;
    private IJSRuntime JsRuntime =>
        _jsRuntime ??= _serviceProvider.GetRequiredService<IJSRuntime>();

    public event Action? OnChange;

    public string CurrentLanguage => _currentLanguage;

    public string GetString(string key)
    {
        if (_translations.TryGetValue(key, out var value))
        {
            return value;
        }
        return key;
    }

    public async Task InitializeAsync()
    {
        string targetLanguage = _currentLanguage;

        try
        {
            var client = _httpClientFactory.CreateClient("PublicClient");
            var response = await client.GetAsync("api/languages");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var availableLanguages = JsonSerializer.Deserialize<List<LanguageDto>>(jsonString, options);

                if (availableLanguages != null && availableLanguages.Count > 0)
                {
                    var cookieValue = await _cookieService.GetCookieAsync("language");
                    var matchedLanguage = availableLanguages.FirstOrDefault(l => l.Code.Equals(cookieValue, StringComparison.OrdinalIgnoreCase));

                    if (matchedLanguage != null)
                    {
                        targetLanguage = matchedLanguage.Code;
                    }
                    else
                    {
                        var browserLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                        var browserMatch = availableLanguages.FirstOrDefault(l => l.Code.Equals(browserLang, StringComparison.OrdinalIgnoreCase));

                        if (browserMatch != null)
                        {
                            targetLanguage = browserMatch.Code;
                        }
                        else
                        {
                            targetLanguage = availableLanguages.FirstOrDefault(l => l.Code.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase))?.Code
                                             ?? availableLanguages.First().Code;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            targetLanguage = DefaultLanguage;
            AppLog.Error(ex, "Unknown error");
        }

        await SetLanguageAsync(targetLanguage);
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
        catch (Exception ex)
        {
            _translations = [];
            _loadedLanguage = null;
            AppLog.Error(ex, "Unknown error");
        }

        OnChange?.Invoke();
    }

    public async Task SetLanguageAsync(string language)
    {
        if (string.IsNullOrEmpty(language)) return;

        if (_loadedLanguage == language && _translations.Count > 0)
        {
            return;
        }
        
        _currentLanguage = language;

        try
        {
            await _cookieService.SetCookieAsync("language", language, days: 365);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Unknown error");
        }
        try
        {
            await JsRuntime.InvokeVoidAsync("appInterop.setDocumentLanguage", language);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Unknown error");
        }

        if (_loadingTask != null && !_loadingTask.IsCompleted)
        {
            await _loadingTask;
            return;
        }

        _loadingTask = FetchTranslationsAsync(_currentLanguage);
        await _loadingTask;
    }
}