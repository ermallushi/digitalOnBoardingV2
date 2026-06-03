using DigitalOnBoardingV2.Api.Models;

namespace DigitalOnBoardingV2.Api.Services;

public interface IDocumentVerificationService
{
    Task<DocumentVerificationResult> VerifyAsync(SubmitDocumentRequest request, string customerFullName);
}

/// <summary>
/// Simulates OCR extraction, authenticity checks, and tamper detection.
/// In production: integrates with document scanning vendor (e.g., Onfido, Jumio, Mitek).
/// </summary>
public sealed class DocumentVerificationService : IDocumentVerificationService
{
    public Task<DocumentVerificationResult> VerifyAsync(SubmitDocumentRequest request, string customerFullName)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isExpired = request.ExpiryDate <= today;

        // Simulate: image must be present and document must not be expired
        var isAuthentic = !isExpired && request.DocumentImageBase64.Length > 0;

        // Simulate tamper detection — in production: ML model or vendor API
        var isTampered = false;

        string? rejectionReason = null;
        if (isExpired)
            rejectionReason = "Document is expired.";
        else if (!isAuthentic)
            rejectionReason = "Document could not be authenticated.";

        // Simulate OCR: extract name (production: NLP/OCR model extracts from image)
        var extractedName = customerFullName;

        return Task.FromResult(new DocumentVerificationResult(
            request.DocumentType,
            request.DocumentNumber,
            request.IssuingCountry,
            request.ExpiryDate,
            extractedName,
            isAuthentic,
            isTampered,
            rejectionReason,
            DateTimeOffset.UtcNow));
    }
}
