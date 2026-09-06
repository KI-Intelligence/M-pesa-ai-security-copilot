using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MpesaAiCopilot.Api.Features.Ai.Contracts;

namespace MpesaAiCopilot.Api.Features.Ai.Providers;

public class OpenAiClient: IAiClient
{
    private const string OpenAiUrl = "https://api.openai.com/v1/responses";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public OpenAiClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }


    private string GetApiKey()
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey)) {

            throw new InvalidOperationException("Open API key is not configured");
        }

        return apiKey;
    }




    public async Task<AiResponse> ChatAsync(string message)
    {
        var apiKey = GetApiKey();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            OpenAiUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        var body = new
        {
            model = "gpt-5.6",
            input = message
        };

        var json = JsonSerializer.Serialize(body);

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();

        //response.EnsureSuccessStatusCode();

        //return responseBody;

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI returned {(int)response.StatusCode} ({response.StatusCode}): {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);

        var text = document
            .RootElement
            .GetProperty("output")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return new AiResponse(text ?? string.Empty);
    }

}