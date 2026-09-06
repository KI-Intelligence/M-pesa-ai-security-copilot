using System.Text;
using System.Text.Json;
using MpesaAiCopilot.Api.Features.Ai.Contracts;
namespace MpesaAiCopilot.Api.Features.Ai.Providers;

public class GeminiClient: IAiClient
{
    private const string GeminiUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    private string GetApiKey()
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured.");
        }

        return apiKey;
    }

    public async Task<AiResponse> ChatAsync(string message)
    {
        var apiKey = GetApiKey();

        var url = $"{GeminiUrl}?key={apiKey}";

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = message
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(body);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            url);

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response =
            await _httpClient.SendAsync(request);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini returned {(int)response.StatusCode} " +
                $"({response.StatusCode}): {responseBody}");
        }

        using var document =
            JsonDocument.Parse(responseBody);

        var text = document
            .RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return new AiResponse(text ?? string.Empty);

        
    }
}