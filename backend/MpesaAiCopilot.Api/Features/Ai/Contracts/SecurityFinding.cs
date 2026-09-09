namespace MpesaAiCopilot.Api.Features.Ai.Contracts;

public record SecurityFinding(
    string Title,
    string Severity,
    string Concept,
    string Explanation,
    string Scenario,
    List<string> Recommendations
);