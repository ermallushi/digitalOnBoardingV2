using DigitalOnBoardingV2.Api.Models;
using DigitalOnBoardingV2.Api.Services;

namespace DigitalOnBoardingV2.Api.Endpoints;

public static class SignatureEndpoints
{
    public static IEndpointRouteBuilder MapSignatureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/signatures");

        group.MapPost("/", CreateSignatureRequest).WithName("CreateSignatureRequest");
        group.MapGet("/{id:guid}", GetSignatureRequest).WithName("GetSignatureRequest");
        group.MapPost("/{id:guid}/sign", SignDocument).WithName("SignDocument");
        group.MapGet("/{id:guid}/validate", ValidateSignature).WithName("ValidateSignature");

        return app;
    }

    private static IResult CreateSignatureRequest(
        CreateSignatureRequestDto request,
        ISignatureStore store,
        IDigitalSignatureService sigService,
        IOnboardingSessionStore sessionStore,
        IAuditService audit)
    {
        var error = Validation.Validate(request);
        if (error is not null) return error;

        var session = sessionStore.Get(request.SessionId);
        if (session is null)
            return Results.NotFound(new { message = "Onboarding session not found." });

        if (session.Status != VerificationStatus.Approved && session.Status != VerificationStatus.RequiresReview)
            return Results.Conflict(new { message = "A signature request can only be created for an approved or under-review session." });

        var docHash = sigService.ComputeDocumentHash(request.DocumentContent);

        var sigRequest = new SignatureRequest
        {
            SessionId = request.SessionId,
            DocumentName = request.DocumentName.Trim(),
            DocumentHash = docHash,
            Level = request.Level,
            Signatories = request.Signatories,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(Math.Max(1, request.ExpiryDays))
        };

        store.Add(sigRequest);
        audit.Log(request.SessionId, "SIGNATURE_REQUEST_CREATED",
            session.CustomerEmail, session.Channel.ToString(),
            $"Document='{request.DocumentName}', Level={request.Level}, Signatories={request.Signatories.Count}");

        return Results.Created($"/api/signatures/{sigRequest.Id}", sigRequest);
    }

    private static IResult GetSignatureRequest(Guid id, ISignatureStore store)
    {
        var req = store.Get(id);
        return req is null ? Results.NotFound() : Results.Ok(req);
    }

    private static IResult SignDocument(
        Guid id,
        SignDocumentRequest request,
        ISignatureStore store,
        IDigitalSignatureService sigService,
        IOnboardingSessionStore sessionStore,
        IAuditService audit)
    {
        var error = Validation.Validate(request);
        if (error is not null) return error;

        var sigReq = store.Get(id);
        if (sigReq is null) return Results.NotFound();

        if (sigReq.Status is SignatureStatus.Completed or SignatureStatus.Revoked)
            return Results.Conflict(new { message = "This signature request is already completed or has been revoked." });

        if (sigReq.ExpiresAt < DateTimeOffset.UtcNow)
        {
            sigReq.Status = SignatureStatus.Expired;
            return Results.Conflict(new { message = "This signature request has expired." });
        }

        if (!sigReq.Signatories.Any(s => string.Equals(s, request.Signatory, StringComparison.OrdinalIgnoreCase)))
            return Results.Problem("Signatory is not authorized to sign this document.", statusCode: 403);

        if (sigReq.Signatures.Any(s => string.Equals(s.Signatory, request.Signatory, StringComparison.OrdinalIgnoreCase)))
            return Results.Conflict(new { message = "This signatory has already signed the document." });

        // Tamper detection: verify document content hasn't changed since the request was created
        var currentHash = sigService.ComputeDocumentHash(request.DocumentContent);
        if (!string.Equals(currentHash, sigReq.DocumentHash, StringComparison.Ordinal))
            return Results.Conflict(new { message = "Document content has been altered since the signature request was created." });

        var record = sigService.Sign(request.Signatory, request.DocumentContent, sigReq.Level);
        sigReq.Signatures.Add(record);

        var session = sessionStore.Get(sigReq.SessionId);
        audit.Log(sigReq.SessionId, "DOCUMENT_SIGNED",
            request.Signatory, session?.Channel.ToString() ?? "Unknown",
            $"SignatureRequest={sigReq.Id}, Document='{sigReq.DocumentName}'");

        // Check if all required signatories have now signed
        var signedSet = sigReq.Signatures
            .Select(s => s.Signatory)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (sigReq.Signatories.All(s => signedSet.Contains(s)))
        {
            sigReq.Status = SignatureStatus.Completed;
            sigReq.SignedAt = DateTimeOffset.UtcNow;
            sigReq.DocumentSeal = sigService.ComputeDocumentSeal(sigReq);
            audit.Log(sigReq.SessionId, "DOCUMENT_SIGNING_COMPLETED",
                "system", session?.Channel.ToString() ?? "Unknown",
                $"SignatureRequest={sigReq.Id}, Seal={sigReq.DocumentSeal[..8]}...");
        }
        else
        {
            sigReq.Status = SignatureStatus.PartiallySigned;
        }

        return Results.Ok(record);
    }

    private static IResult ValidateSignature(Guid id, ISignatureStore store)
    {
        var sigReq = store.Get(id);
        if (sigReq is null) return Results.NotFound();

        if (sigReq.Status != SignatureStatus.Completed)
            return Results.Ok(new { Valid = false, Reason = "Document signing is not yet complete." });

        return Results.Ok(new
        {
            Valid = !string.IsNullOrEmpty(sigReq.DocumentSeal),
            SignatureRequestId = sigReq.Id,
            Level = sigReq.Level.ToString(),
            SignedAt = sigReq.SignedAt,
            ExpiresAt = sigReq.ExpiresAt,
            Signatories = sigReq.Signatories,
            CertificateIds = sigReq.Signatures.Select(s => s.Certificate).ToArray()
        });
    }
}
