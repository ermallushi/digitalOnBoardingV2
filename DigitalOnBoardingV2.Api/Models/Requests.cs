using System.ComponentModel.DataAnnotations;

namespace DigitalOnBoardingV2.Api.Models;

public sealed class StartOnboardingRequest
{
    [Required]
    public Guid CustomerId { get; init; }

    [Required, MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(250)]
    public string Email { get; init; } = string.Empty;

    public OnboardingChannel Channel { get; init; } = OnboardingChannel.Digital;
}

public sealed class StartInstoreSessionRequest
{
    [Required]
    public Guid CustomerId { get; init; }

    [Required, MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(250)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string StaffId { get; init; } = string.Empty;

    [MaxLength(50)]
    public string? LocationCode { get; init; }
}

public sealed class SubmitDocumentRequest
{
    [Required]
    public DocumentType DocumentType { get; init; }

    [Required, MaxLength(100)]
    public string DocumentNumber { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string IssuingCountry { get; init; } = string.Empty;

    [Required]
    public DateOnly ExpiryDate { get; init; }

    // Base64-encoded document image; in production this would be a multipart upload
    [Required, MaxLength(2_000_000)]
    public string DocumentImageBase64 { get; init; } = string.Empty;
}

public sealed class SubmitBiometricRequest
{
    // Base64-encoded selfie image
    [Required, MaxLength(2_000_000)]
    public string SelfieImageBase64 { get; init; } = string.Empty;

    public bool FingerprintProvided { get; init; }
}

public sealed class InstoreScanRequest
{
    [Required]
    public DocumentType DocumentType { get; init; }

    [Required, MaxLength(100)]
    public string DocumentNumber { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string IssuingCountry { get; init; } = string.Empty;

    [Required]
    public DateOnly ExpiryDate { get; init; }

    [Required, MaxLength(2_000_000)]
    public string DocumentImageBase64 { get; init; } = string.Empty;

    [Required, MaxLength(2_000_000)]
    public string SelfieImageBase64 { get; init; } = string.Empty;
}

public sealed class CreateSignatureRequestDto
{
    [Required]
    public Guid SessionId { get; init; }

    [Required, MaxLength(300)]
    public string DocumentName { get; init; } = string.Empty;

    [Required, MaxLength(1_000_000)]
    public string DocumentContent { get; init; } = string.Empty;

    public SignatureLevel Level { get; init; } = SignatureLevel.Advanced;

    [Required, MinLength(1)]
    public IReadOnlyList<string> Signatories { get; init; } = [];

    public int ExpiryDays { get; init; } = 7;
}

public sealed class SignDocumentRequest
{
    [Required, MaxLength(250)]
    public string Signatory { get; init; } = string.Empty;

    [Required, MaxLength(1_000_000)]
    public string DocumentContent { get; init; } = string.Empty;
}
