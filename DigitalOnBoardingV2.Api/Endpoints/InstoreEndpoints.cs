using DigitalOnBoardingV2.Api.Models;
using DigitalOnBoardingV2.Api.Services;

namespace DigitalOnBoardingV2.Api.Endpoints;

public static class InstoreEndpoints
{
    public static IEndpointRouteBuilder MapInstoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/instore");

        group.MapPost("/sessions", StartInstoreSession).WithName("StartInstoreSession");
        group.MapGet("/sessions/{id:guid}", GetInstoreSession).WithName("GetInstoreSession");
        group.MapPost("/sessions/{id:guid}/scan", ScanAndVerify).WithName("ScanAndVerify");

        return app;
    }

    private static IResult StartInstoreSession(
        StartInstoreSessionRequest request,
        IOnboardingSessionStore store,
        IAuditService audit)
    {
        var error = Validation.Validate(request);
        if (error is not null) return error;

        var session = new OnboardingSession
        {
            CustomerId = request.CustomerId,
            CustomerFullName = request.FullName.Trim(),
            CustomerEmail = request.Email.Trim(),
            Channel = OnboardingChannel.InStore,
            StaffId = request.StaffId.Trim(),
            LocationCode = request.LocationCode?.Trim()
        };

        store.Add(session);
        audit.Log(session.Id, "INSTORE_SESSION_STARTED", request.StaffId, OnboardingChannel.InStore.ToString(),
            $"Location={request.LocationCode}");

        return Results.Created($"/api/instore/sessions/{session.Id}", session);
    }

    private static IResult GetInstoreSession(Guid id, IOnboardingSessionStore store)
    {
        var session = store.Get(id);
        return session is null ? Results.NotFound() : Results.Ok(session);
    }

    private static async Task<IResult> ScanAndVerify(
        Guid id,
        InstoreScanRequest request,
        IOnboardingSessionStore store,
        IDocumentVerificationService docService,
        IBiometricService biometricService,
        IRiskScoringService riskService,
        IAuditService audit)
    {
        var error = Validation.Validate(request);
        if (error is not null) return error;

        var session = store.Get(id);
        if (session is null) return Results.NotFound();

        if (session.Status != VerificationStatus.Pending)
            return Results.Conflict(new { message = "Verification has already been performed for this session." });

        // Step 1: Document verification (OCR + authenticity)
        var docRequest = new SubmitDocumentRequest
        {
            DocumentType = request.DocumentType,
            DocumentNumber = request.DocumentNumber,
            IssuingCountry = request.IssuingCountry,
            ExpiryDate = request.ExpiryDate,
            DocumentImageBase64 = request.DocumentImageBase64
        };

        var docResult = await docService.VerifyAsync(docRequest, session.CustomerFullName);
        session.DocumentResult = docResult;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        if (!docResult.IsAuthentic || docResult.IsTampered)
        {
            session.Status = VerificationStatus.Rejected;
            audit.Log(session.Id, "INSTORE_DOCUMENT_REJECTED", session.StaffId ?? "unknown",
                OnboardingChannel.InStore.ToString(), docResult.RejectionReason);
            return Results.Ok(new InstoreVerificationResult(
                session.Id, false, docResult, null, null, session.Status, docResult.RejectionReason));
        }

        // Step 2: Biometric verification (facial match + liveness)
        var bioRequest = new SubmitBiometricRequest
        {
            SelfieImageBase64 = request.SelfieImageBase64,
            FingerprintProvided = false
        };

        var bioResult = await biometricService.VerifyAsync(bioRequest);
        session.BiometricResult = bioResult;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        if (!bioResult.FaceMatchPassed || !bioResult.LivenessCheckPassed)
        {
            session.Status = VerificationStatus.Rejected;
            audit.Log(session.Id, "INSTORE_BIOMETRIC_REJECTED", session.StaffId ?? "unknown",
                OnboardingChannel.InStore.ToString(), bioResult.RejectionReason);
            return Results.Ok(new InstoreVerificationResult(
                session.Id, false, docResult, bioResult, null, session.Status, bioResult.RejectionReason));
        }

        // Step 3: Risk assessment (AML/CTF screening)
        session.Status = VerificationStatus.BiometricVerified;
        var riskResult = await riskService.AssessAsync(session);
        session.RiskResult = riskResult;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        if (!riskResult.SanctionsScreeningPassed || riskResult.RiskLevel == RiskLevel.Critical)
        {
            session.Status = VerificationStatus.Rejected;
            audit.Log(session.Id, "INSTORE_RISK_REJECTED", session.StaffId ?? "unknown",
                OnboardingChannel.InStore.ToString(),
                $"RiskLevel={riskResult.RiskLevel}, Flags={string.Join(",", riskResult.Flags)}");
        }
        else if (riskResult.RiskLevel == RiskLevel.High)
        {
            session.Status = VerificationStatus.RequiresReview;
            audit.Log(session.Id, "INSTORE_REQUIRES_REVIEW", session.StaffId ?? "unknown",
                OnboardingChannel.InStore.ToString(), $"RiskLevel={riskResult.RiskLevel}");
        }
        else
        {
            session.Status = VerificationStatus.Approved;
            audit.Log(session.Id, "INSTORE_APPROVED", session.StaffId ?? "unknown",
                OnboardingChannel.InStore.ToString(), $"RiskScore={riskResult.RiskScore}");
        }

        var approved = session.Status == VerificationStatus.Approved;
        return Results.Ok(new InstoreVerificationResult(
            session.Id, approved, docResult, bioResult, riskResult, session.Status, null));
    }
}

public sealed record InstoreVerificationResult(
    Guid SessionId,
    bool Approved,
    DocumentVerificationResult DocumentResult,
    BiometricVerificationResult? BiometricResult,
    RiskAssessmentResult? RiskResult,
    VerificationStatus Status,
    string? RejectionReason);
