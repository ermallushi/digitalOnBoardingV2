using DigitalOnBoardingV2.Api.Models;

namespace DigitalOnBoardingV2.Api.Services;

public interface IRiskScoringService
{
    Task<RiskAssessmentResult> AssessAsync(OnboardingSession session);
}

/// <summary>
/// Simulates real-time AML/CTF screening: sanctions list, PEP database, and adverse media checks.
/// Computes a configurable risk score and assigns a risk level.
/// In production: integrates with providers like Dow Jones, Refinitiv, ComplyAdvantage, or LexisNexis.
/// </summary>
public sealed class RiskScoringService : IRiskScoringService
{
    // Simulated sanctions / PEP lists — production would query live vendor APIs
    private static readonly HashSet<string> SanctionedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "John Doe Sanctioned", "Jane Blocked"
    };

    private static readonly HashSet<string> PepNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Political Person Example"
    };

    public Task<RiskAssessmentResult> AssessAsync(OnboardingSession session)
    {
        var name = session.CustomerFullName;
        var flags = new List<string>();

        var sanctionsPassed = !SanctionedNames.Contains(name);
        var pepPassed = !PepNames.Contains(name);
        // Adverse media: simulated as always clean; production would query news/media APIs
        var adverseMediaPassed = true;

        if (!sanctionsPassed) flags.Add("SANCTIONS_MATCH");
        if (!pepPassed) flags.Add("PEP_MATCH");
        if (!adverseMediaPassed) flags.Add("ADVERSE_MEDIA");

        // Risk score (0–100): weighted by screening outcomes
        double score = 0;
        if (!sanctionsPassed) score += 80;
        if (!pepPassed) score += 40;
        if (!adverseMediaPassed) score += 20;

        // Small uplift for non-domestic documents (cross-border risk factor)
        if (session.DocumentResult is { } doc &&
            !string.Equals(doc.IssuingCountry, "AL", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        score = Math.Min(score, 100);

        var riskLevel = score switch
        {
            >= 80 => RiskLevel.Critical,
            >= 50 => RiskLevel.High,
            >= 20 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        return Task.FromResult(new RiskAssessmentResult(
            sanctionsPassed,
            pepPassed,
            adverseMediaPassed,
            riskLevel,
            Math.Round(score, 2),
            flags.AsReadOnly(),
            DateTimeOffset.UtcNow));
    }
}
