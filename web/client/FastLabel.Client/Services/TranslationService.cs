using System.Net.Http.Json;
using FastLabel.Shared.DTOs;

public class TranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentCulture = "fr_fr";

    public event Action? OnChange;

    public TranslationService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task InitializeAsync(string culture)
    {
        _currentCulture = culture.ToLower();
        Console.WriteLine($"[TranslationService] 🟢 Début InitializeAsync pour la culture : {_currentCulture}");

        try
        {
            var client = _httpClientFactory.CreateClient("PublicClient");
            var url = $"api/translations/{_currentCulture}";
            
            var result = await client.GetFromJsonAsync<List<TranslationDto>>(url);
            
            if (result != null)
            {
                _translations = result.ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
                Console.WriteLine($"[TranslationService] ✅ Succès : {_translations.Count} traductions chargées en mémoire.");
                
                // Afficher un aperçu des clés chargées pour vérifier
                foreach (var kvp in _translations.Take(3))
                {
                    Console.WriteLine($"[TranslationService] 📝 Exemple -> Clé: '{kvp.Key}' | Valeur: '{kvp.Value}'");
                }
            }
            else
            {
                Console.WriteLine("[TranslationService] ⚠️ Attention : L'API a retourné un résultat NULL.");
                _translations = [];
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TranslationService] ❌ ERREUR lors de l'appel HTTP : {ex.Message}");
            _translations = [];
        }

        Console.WriteLine("[TranslationService] 🔔 Déclenchement de l'événement OnChange.");
        OnChange?.Invoke();
    }

    public string GetString(string key)
    {
        if (_translations.TryGetValue(key, out var value))
        {
            return value;
        }
        
        // Log pour voir si une clé demandée par l'UI n'existe pas dans le dictionnaire
        Console.WriteLine($"[TranslationService] 🔍 Clé introuvable dans le dictionnaire : '{key}' (Culture actuelle: {_currentCulture})");
        return key; 
    }
}