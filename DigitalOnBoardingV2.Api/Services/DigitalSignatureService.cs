using System.Security.Cryptography;
using System.Text;
using DigitalOnBoardingV2.Api.Models;

namespace DigitalOnBoardingV2.Api.Services;

public interface IDigitalSignatureService
{
    /// <summary>Returns a SHA-256 hex digest of the document content.</summary>
    string ComputeDocumentHash(string documentContent);

    /// <summary>
    /// Creates a signature record for the given signatory.
    /// In production: uses PKI (RSA/ECDSA) with certificates issued by a trusted CA.
    /// Simple signatures use HMAC; Advanced/Qualified signatures would use asymmetric keys.
    /// </summary>
    SignatureRecord Sign(string signatory, string documentContent, SignatureLevel level);

    /// <summary>Computes a tamper-evident seal that covers all collected signatures plus the document hash.</summary>
    string ComputeDocumentSeal(SignatureRequest request);
}

/// <summary>
/// Implements digital signature capabilities aligned with eIDAS (EU) and UETA (US) requirements.
/// Uses HMACSHA256 for the simulation; production would use asymmetric PKI per signatory.
/// </summary>
public sealed class DigitalSignatureService : IDigitalSignatureService
{
    // Shared signing key for the HMAC simulation — in production replaced by per-user private keys
    private static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);

    public string ComputeDocumentHash(string documentContent)
    {
        var bytes = Encoding.UTF8.GetBytes(documentContent);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public SignatureRecord Sign(string signatory, string documentContent, SignatureLevel level)
    {
        var docHash = ComputeDocumentHash(documentContent);
        var payload = $"{signatory}:{docHash}:{DateTimeOffset.UtcNow:O}";

        using var hmac = new HMACSHA256(SigningKey);
        var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signatureHash = Convert.ToHexString(sigBytes).ToLowerInvariant();

        // Certificate stub — production: issued by a trusted CA for Advanced/Qualified levels
        var certificate = $"CERT-{level.ToString().ToUpperInvariant()}-{Math.Abs(signatory.GetHashCode()):X8}";

        return new SignatureRecord(signatory, signatureHash, certificate, DateTimeOffset.UtcNow);
    }

    public string ComputeDocumentSeal(SignatureRequest request)
    {
        // Seal = HMAC over (all signature hashes + document hash) — tamper-evident
        var combined = string.Join("|", request.Signatures.Select(s => s.SignatureHash));
        combined += $"|{request.DocumentHash}";

        using var hmac = new HMACSHA256(SigningKey);
        var sealBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(sealBytes).ToLowerInvariant();
    }
}
