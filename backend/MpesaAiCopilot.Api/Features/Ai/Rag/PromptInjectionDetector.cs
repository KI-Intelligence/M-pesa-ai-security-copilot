namespace MpesaAiCopilot.Api.Features.Ai.Rag;

public static class PromptInjectionDetector
{
    private static readonly string[] SuspiciousPhrases =
    [
        "ignore all previous instructions",
        "ignore previous instructions",
        "disregard the above",
        "disregard all previous",
        "you must respond only with",
        "do not deviate from this instruction",
        "system prompt",
        "override your instructions",
        "act as if",
        "new instructions:",
    ];

    public static bool ContainsSuspiciousInstructions(string content)
    {
        var normalized = content.ToLowerInvariant();

        return SuspiciousPhrases.Any(phrase => normalized.Contains(phrase));
    }
}