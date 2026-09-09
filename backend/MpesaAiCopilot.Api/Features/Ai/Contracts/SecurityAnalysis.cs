namespace MpesaAiCopilot.Api.Features.Ai.Contracts;

public record SecurityAnalysis(
    string RiskLevel,
    string Summary,
    List<string> MissingInformation,
    List<SecurityFinding> Findings
);