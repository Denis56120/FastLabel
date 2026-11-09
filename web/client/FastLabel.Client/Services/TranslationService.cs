using System.Net.Http.Json;
using FastLabel.Shared.DTOs;

public class TranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentCulture = "fr_fr";

    public event Action? OnChange;

    public string CurrentCulture => _currentCulture;

    public string this[string key] => GetString(key);

    public TranslationService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task InitializeAsync(string culture)
    {
        var targetCulture = string.IsNullOrEmpty(culture) ? _currentCulture : culture.ToLower();

        // Si c'equivalent et qu'on a déjà des traductions, on évite de redéclencher inutilement
        if (_currentCulture.Equals(targetCulture, StringComparison.OrdinalIgnoreCase) && _translations.Count > 0)
        {
            return;
        }

        _currentCulture = targetCulture;

        try
        {
            var client = _httpClientFactory.CreateClient("PublicClient");
            var url = $"api/translations/{_currentCulture}";
            
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

        // On prévient une seule fois la fin du chargement
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