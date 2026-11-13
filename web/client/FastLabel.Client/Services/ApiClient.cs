namespace FastLabel.Client.Services;

public class ApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<(bool IsSuccess, string? ErrorMessage)> PostAsync<TRequest>(string endpoint, TRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(endpoint, request);
        
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        return await ParseErrorResponseAsync(response);
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> PutAsync<TRequest>(string endpoint, TRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync(endpoint, request);
        
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        return await ParseErrorResponseAsync(response);
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> DeleteAsync(string endpoint)
    {
        var response = await _httpClient.DeleteAsync(endpoint);
        
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        return await ParseErrorResponseAsync(response);
    }

    public async Task<T?> GetFromJsonAsync<T>(string endpoint)
    {
        return await _httpClient.GetFromJsonAsync<T>(endpoint);
    }

    private static async Task<(bool, string?)> ParseErrorResponseAsync(HttpResponseMessage response)
    {
        try
        {
            var errorDto = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
            if (errorDto != null && !string.IsNullOrEmpty(errorDto.Message))
            {
                return (false, errorDto.Message);
            }
        }
        catch
        {
        }

        return (false, "app.error.unknown");
    }
}