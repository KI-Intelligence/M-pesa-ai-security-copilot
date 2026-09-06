namespace MpesaAiCopilot.Api.Features.Ai
{
    public interface IAiClient
    {
        Task<AiResponse> ChatAsync(string message);
    }
}
