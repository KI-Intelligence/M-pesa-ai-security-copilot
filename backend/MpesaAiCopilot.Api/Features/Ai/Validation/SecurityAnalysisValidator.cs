using MpesaAiCopilot.Api.Features.Ai.Contracts;

namespace MpesaAiCopilot.Api.Features.Ai.Validation;

public static class SecurityAnalysisValidator
{
    public static ValidatedSecurityAnalysis Validate(SecurityAnalysis raw)
    {
        var warnings = new List<string>();

        var riskLevel = ParseSeverity(raw.RiskLevel, "riskLevel", warnings);

        var findings = raw.Findings
            .Select(f => new ValidatedSecurityFinding(
                f.Title,
                ParseSeverity(f.Severity, $"findings[\"{f.Title}\"].severity", warnings),
                f.Concept,
                f.Explanation,
                f.Scenario,
                f.Recommendations))
            .ToList();

        return new ValidatedSecurityAnalysis(
            riskLevel,
            raw.Summary,
            raw.MissingInformation,
            findings,
            warnings);
    }

    private static RiskSeverity ParseSeverity(
        string? rawValue,
        string fieldName,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            warnings.Add($"{fieldName} was empty; defaulted to Critical.");
            return RiskSeverity.Critical;
        }

        var normalized = rawValue.Trim().ToLowerInvariant();

        if (normalized.Contains("critical"))
        {
            return RiskSeverity.Critical;
        }

        if (normalized.Contains("high"))
        {
            return RiskSeverity.High;
        }

        if (normalized.Contains("medium"))
        {
            return RiskSeverity.Medium;
        }

        if (normalized.Contains("low"))
        {
            return RiskSeverity.Low;
        }

        warnings.Add($"{fieldName} had an unrecognized value '{rawValue}'; defaulted to Critical.");
        return RiskSeverity.Critical;
    }
}