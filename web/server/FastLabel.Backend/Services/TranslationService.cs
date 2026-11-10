using System.Net.Http.Json;
using FastLabel.Shared.DTOs;

namespace FastLabel.Client.Services;

public class TranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = string.Empty;

    // Événement pour notifier les composants des changements de langue/traductions
    public event Action? OnChange;

    public TranslationService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task InitializeAsync(string language)
    {
        // Évite de recharger si on a déjà les traductions pour cette langue
        if (_currentLanguage == language && _translations.Count > 0)
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("PublicClient");
            var dtos = await client.GetFromJsonAsync<List<TranslationDto>>($"api/translations/{language}");

            if (dtos != null && dtos.Count > 0)
            {
                // 1. On remplit le dictionnaire en mémoire
                _translations = dtos.ToDictionary(d => d.Key, d => d.Value, StringComparer.OrdinalIgnoreCase);
                _currentLanguage = language;

                // 2. ON FORCE LE RAFRAÎCHISSEMENT UNIQUEMENT MAINTENANT
                // Notifie les composants abonnés pour qu'ils affichent les textes d'un coup sec
                OnChange?.Invoke();
            }
        }
        catch (Exception)
        {
        }
    }

    public string GetString(string key)
    {
        // 1. Si la traduction existe dans la langue actuelle, on la retourne
        if (_translations.TryGetValue(key, out var value))
        {
            return value;
        }

        // 2. Sinon, on retourne une chaîne vide pour éviter tout clignotement ou affichage de tag technique
        return string.Empty;
    }
}