namespace MpesaAiCopilot.Api.Features.Ai.Contracts;

public interface IAiClient
{
    Task<AiResponse> ChatAsync(string message);
}
