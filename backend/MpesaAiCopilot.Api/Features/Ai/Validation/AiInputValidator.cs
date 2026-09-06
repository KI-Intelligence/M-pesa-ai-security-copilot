namespace MpesaAiCopilot.Api.Features.Ai.Validation
{
    public static class AiInputValidator
    {
         private const int MaxMessageLength = 4000;

        public  static void Validate (string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message cannot be null or empty.");
            }
            if (message.Length > MaxMessageLength)
            {
                throw new ArgumentException($"Message cannot exceed {MaxMessageLength} characters.");
            }
        }
    }
}
