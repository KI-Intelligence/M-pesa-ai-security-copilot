namespace MpesaAiCopilot.Api.Features.Ai.Contracts;

public record ValidatedSecurityFinding(
    string Title,
    RiskSeverity Severity,
    string Concept,
    string Explanation,
    string Scenario,
    List<string> Recommendations
);

public record ValidatedSecurityAnalysis(
    RiskSeverity RiskLevel,
    string Summary,
    List<string> MissingInformation,
    List<ValidatedSecurityFinding> Findings,
    List<string> ValidationWarnings
);