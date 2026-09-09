using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MpesaAiCopilot.Api.Features.Ai.Contracts;

namespace MpesaAiCopilot.Api.Features.Ai.Providers;

public class AnthropicClient : IAiClient
{
    private const string AnthropicUrl =
        "https://api.anthropic.com/v1/messages";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AnthropicClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    private string GetApiKey()
    {
        var apiKey = _configuration["Anthropic:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Anthropic API key is not configured.");
        }

        return apiKey;
    }

    public async Task<AiResponse> ChatAsync(string message)
    {
        var apiKey = GetApiKey();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            AnthropicUrl);

        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var body = new
        {
            model = "claude-sonnet-4-5",
            max_tokens = 1024,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = message
                }
            }
        };

        var json = JsonSerializer.Serialize(body);

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
                $"Anthropic returned {(int)response.StatusCode} " +
                $"({response.StatusCode}): {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);

        var text = document
            .RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return new AiResponse(text ?? string.Empty);
    }



    public Task<SecurityAnalysis> AnalyzeAsync(string message)
    {
        throw new NotImplementedException(
            "Structured security analysis is not implemented for Anthropic yet.");
    }
}