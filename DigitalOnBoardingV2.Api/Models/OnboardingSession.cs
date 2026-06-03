namespace DigitalOnBoardingV2.Api.Models;

/// <summary>
/// Represents a KYC onboarding session for a customer, across either the digital or in-store channel.
/// </summary>
public sealed class OnboardingSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public OnboardingChannel Channel { get; init; }
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;

    // In-store specific fields
    public string? StaffId { get; init; }
    public string? LocationCode { get; init; }

    // Step results
    public DocumentVerificationResult? DocumentResult { get; set; }
    public BiometricVerificationResult? BiometricResult { get; set; }
    public RiskAssessmentResult? RiskResult { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record DocumentVerificationResult(
    DocumentType DocumentType,
    string DocumentNumber,
    string IssuingCountry,
    DateOnly ExpiryDate,
    string ExtractedFullName,
    bool IsAuthentic,
    bool IsTampered,
    string? RejectionReason,
    DateTimeOffset VerifiedAt);

public sealed record BiometricVerificationResult(
    bool FaceMatchPassed,
    double FaceMatchScore,
    bool LivenessCheckPassed,
    bool FingerprintVerified,
    string? RejectionReason,
    DateTimeOffset VerifiedAt);

public sealed record RiskAssessmentResult(
    bool SanctionsScreeningPassed,
    bool PepCheckPassed,
    bool AdverseMediaCheckPassed,
    RiskLevel RiskLevel,
    double RiskScore,
    IReadOnlyList<string> Flags,
    DateTimeOffset AssessedAt);
