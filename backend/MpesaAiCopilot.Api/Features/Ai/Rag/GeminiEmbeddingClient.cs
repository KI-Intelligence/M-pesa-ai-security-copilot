using System.Net.Http.Json;
using System.Text.Json;

namespace MpesaAiCopilot.Api.Features.Ai.Rag;

public class GeminiEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    private const string GeminiEmbeddingUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent";

    public GeminiEmbeddingClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured.");
        }

        var request = new
        {
            content = new
            {
                parts = new[]
                {
                    new
                    {
                        text
                    }
                }
            }
        };

        var url = $"{GeminiEmbeddingUrl}?key={apiKey}";

        var response = await _httpClient.PostAsJsonAsync(url, request);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var values = document
            .RootElement
            .GetProperty("embedding")
            .GetProperty("values");

        return values
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }
}