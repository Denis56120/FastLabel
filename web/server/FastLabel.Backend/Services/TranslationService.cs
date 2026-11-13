namespace FastLabel.Backend.Services;

public class TranslationService(IHttpClientFactory httpClientFactory)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = string.Empty;
    public event Action? OnChange;

    public async Task InitializeAsync(string language)
    {
        if (_currentLanguage == language && _translations.Count > 0)
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("PublicClient");
            var dtos = await client.GetFromJsonAsync<List<TranslationDto>>($"api/translations/{language}");

            if (dtos != null && dtos.Count > 0)
            {
                _translations = dtos.ToDictionary(d => d.Key, d => d.Value, StringComparer.OrdinalIgnoreCase);
                _currentLanguage = language;

                OnChange?.Invoke();
            }
        }
        catch (Exception)
        {
        }
    }

    public string GetString(string key)
    {
        if (_translations.TryGetValue(key, out var value))
        {
            return value;
        }

        return string.Empty;
    }
}