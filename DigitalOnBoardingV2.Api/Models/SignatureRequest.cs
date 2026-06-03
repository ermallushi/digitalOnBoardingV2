namespace DigitalOnBoardingV2.Api.Models;

/// <summary>
/// Represents a digital signature request for one or more signatories on a document.
/// Supports simple, advanced, and qualified electronic signatures (aligned with eIDAS/UETA).
/// </summary>
public sealed class SignatureRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public string DocumentName { get; init; } = string.Empty;
    public string DocumentHash { get; set; } = string.Empty;
    public SignatureLevel Level { get; init; }
    public SignatureStatus Status { get; set; } = SignatureStatus.Pending;
    public IReadOnlyList<string> Signatories { get; init; } = [];
    public List<SignatureRecord> Signatures { get; init; } = [];
    public string? DocumentSeal { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SignedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; init; }
}

public sealed record SignatureRecord(
    string Signatory,
    string SignatureHash,
    string Certificate,
    DateTimeOffset SignedAt);
