using FastLabel.Shared.DTOs;

public class LabelService(IHttpClientFactory clientFactory)
{
    private readonly IHttpClientFactory _clientFactory = clientFactory;

    public async Task<List<LabelDto>> GetLabelsAsync()
    {
        var client = _clientFactory.CreateClient("AuthClient");
        
        var labels = await client.GetFromJsonAsync<List<LabelDto>>("api/labels");
        return labels ?? new();
    }
}