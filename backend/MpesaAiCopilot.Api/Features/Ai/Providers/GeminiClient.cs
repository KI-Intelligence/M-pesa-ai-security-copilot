using System.Text;
using System.Text.Json;
using MpesaAiCopilot.Api.Features.Ai.Contracts;
using MpesaAiCopilot.Api.Features.Ai.Prompts;
using Microsoft.Extensions.Logging;


namespace MpesaAiCopilot.Api.Features.Ai.Providers;

public class GeminiClient : IAiClient
{
    private const string GeminiUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
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
        var body = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                new
                {
                    text = SecurityCopilotPrompt.SystemPrompt
                }
            }
            },

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

        var responseBody = await SendRequestAsync(body);
        var text = ExtractText(responseBody);

        return new AiResponse(text);
    }


    private const int MaxRetries = 3;

    private async Task<string> SendRequestAsync(object body)
    {
        var apiKey = GetApiKey();

        var url = $"{GeminiUrl}?key={apiKey}";

        var json = JsonSerializer.Serialize(body);

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
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

            if (response.IsSuccessStatusCode)
            {
                return responseBody;
            }

            var isRetryable =
                response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;

            if (!isRetryable || attempt == MaxRetries)
            {
                throw new HttpRequestException(
                    $"Gemini returned {(int)response.StatusCode} " +
                    $"({response.StatusCode}): {responseBody}");
            }

            var delaySeconds = Math.Pow(2, attempt);

            _logger.LogWarning(
        "Gemini returned {StatusCode}, retrying in {DelaySeconds}s (attempt {Attempt} of {MaxRetries}).",
        (int)response.StatusCode,
        delaySeconds,
        attempt + 1,
        MaxRetries);

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        throw new InvalidOperationException("Retry loop exited unexpectedly.");
    }

    private static string ExtractText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "Gemini response did not contain any candidates.");
        }

        var candidate = candidates[0];

        if (!candidate.TryGetProperty("content", out var content))
        {
            throw new InvalidOperationException(
                "Gemini response did not contain content.");
        }

        if (!content.TryGetProperty("parts", out var parts) ||
            parts.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "Gemini response did not contain any parts.");
        }

        if (!parts[0].TryGetProperty("text", out var textElement))
        {
            throw new InvalidOperationException(
                "Gemini response did not contain text.");
        }

        return textElement.GetString() ?? string.Empty;
    }

    public async Task<SecurityAnalysis> AnalyzeAsync(string message)
    {
        var body = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                new
                {
                    text = SecurityCopilotPrompt.SystemPrompt
                }
            }
            },

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
        },

            generationConfig = new
            {
                responseMimeType = "application/json",

                responseSchema = new
                {
                    type = "object",

                    properties = new
                    {
                        riskLevel = new
                        {
                            type = "string",
                            description = "Overall security risk level."
                        },

                        summary = new
                        {
                            type = "string",
                            description = "A concise summary of the security assessment."
                        },

                        missingInformation = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "string"
                            }
                        },

                        findings = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",

                                properties = new
                                {
                                    title = new { type = "string" },
                                    severity = new { type = "string" },
                                    concept = new { type = "string" },
                                    explanation = new { type = "string" },
                                    scenario = new { type = "string" },

                                    recommendations = new
                                    {
                                        type = "array",
                                        items = new
                                        {
                                            type = "string"
                                        }
                                    }
                                },

                                required = new[]
                    {
                        "title",
                        "severity",
                        "concept",
                        "explanation",
                        "scenario",
                        "recommendations"
                    }
                            }
                        }
                    },

                    required = new[]
        {
            "riskLevel",
            "summary",
            "missingInformation",
            "findings"
        }
                }
            }
        };

        var responseBody = await SendRequestAsync(body);
        var text = ExtractText(responseBody);

        Console.WriteLine("Gemini raw response text:");
        Console.WriteLine(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "Gemini returned an empty structured response.");
        }

        var analysis = JsonSerializer.Deserialize<SecurityAnalysis>(
            text,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (analysis is null)
        {
            throw new InvalidOperationException(
                "Gemini response could not be converted to SecurityAnalysis.");
        }

        return analysis;
    }

}