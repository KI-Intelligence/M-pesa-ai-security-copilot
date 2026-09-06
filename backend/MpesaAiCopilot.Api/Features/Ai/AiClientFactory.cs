using MpesaAiCopilot.Api.Features.Ai.Contracts;
using MpesaAiCopilot.Api.Features.Ai.Providers;

namespace MpesaAiCopilot.Api.Features.Ai;


public class AiClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly GeminiClient _geminiClient;
    private readonly OpenAiClient _openAiClient;
    private readonly AnthropicClient _anthropicClient;

    public AiClientFactory(
        IConfiguration configuration,
        GeminiClient geminiClient,
        OpenAiClient openAiClient,
        AnthropicClient anthropicClient)
    {
        _configuration = configuration;
        _geminiClient = geminiClient;
        _openAiClient = openAiClient;
        _anthropicClient = anthropicClient;
    }

    public IAiClient Create()
    {
        var provider = _configuration["AI:Provider"];

        return provider switch
        {
            "Gemini" => _geminiClient,
            "OpenAI" => _openAiClient,
            "Anthropic" => _anthropicClient,

            _ => throw new InvalidOperationException(
                $"Unsupported AI provider: {provider}")
        };
    }
}