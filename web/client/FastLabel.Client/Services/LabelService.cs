using FastLabel.Shared.DTOs;

public class LabelService
{
    private readonly IHttpClientFactory _clientFactory;

    public LabelService(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<List<LabelDto>> GetLabelsAsync()
    {
        // Utilise "AuthClient" pour que le token soit injecté automatiquement
        var client = _clientFactory.CreateClient("AuthClient");
        
        var labels = await client.GetFromJsonAsync<List<LabelDto>>("api/labels");
        return labels ?? new();
    }
}