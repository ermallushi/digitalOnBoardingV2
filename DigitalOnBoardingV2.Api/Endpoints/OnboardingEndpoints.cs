using DigitalOnBoardingV2.Api.Models;
using DigitalOnBoardingV2.Api.Services;

namespace DigitalOnBoardingV2.Api.Endpoints;

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/onboarding");

        group.MapPost("/sessions", StartSession).WithName("StartOnboardingSession");
        group.MapGet("/sessions/{id:guid}", GetSession).WithName("GetOnboardingSession");
        group.MapPost("/sessions/{id:guid}/document", SubmitDocument).WithName("SubmitDocument");
        group.MapPost("/sessions/{id:guid}/biometric", SubmitBiometric).WithName("SubmitBiometric");
        group.MapGet("/sessions/{id:guid}/risk-assessment", GetRiskAssessment).WithName("GetRiskAssessment");

        return app;
    }

    private static IResult StartSession(
        StartOnboardingRequest request,
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
            Channel = request.Channel
        };

        store.Add(session);
        audit.Log(session.Id, "SESSION_STARTED", session.CustomerEmail, request.Channel.ToString());

        return Results.Created($"/api/onboarding/sessions/{session.Id}", session);
    }

    private static IResult GetSession(Guid id, IOnboardingSessionStore store)
    {
        var session = store.Get(id);
        return session is null ? Results.NotFound() : Results.Ok(session);
    }

    private static async Task<IResult> SubmitDocument(
        Guid id,
        SubmitDocumentRequest request,
        IOnboardingSessionStore store,
        IDocumentVerificationService docService,
        IAuditService audit)
    {
        var error = Validation.Validate(request);
        if (error is not null) return error;

        var session = store.Get(id);
        if (session is null) return Results.NotFound();

        if (session.Status >= VerificationStatus.DocumentSubmitted)
            return Results.Conflict(new { message = "Document has already been submitted for this session." });

        var result = await docService.VerifyAsync(request, session.CustomerFullName);
        session.DocumentResult = result;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        if (!result.IsAuthentic || result.IsTampered)
        {
            session.Status = VerificationStatus.Rejected;
            audit.Log(session.Id, "DOCUMENT_REJECTED", session.CustomerEmail, session.Channel.ToString(),
                result.RejectionReason);
        }
        else
        {
            session.Status = VerificationStatus.DocumentSubmitted;
            audit.Log(session.Id, "DOCUMENT_VERIFIED", session.CustomerEmail, session.Channel.ToString(),
                $"Type={result.DocumentType}, Country={result.IssuingCountry}");
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> SubmitBiometric(
        Guid id,
        SubmitBiometricRequest request,
        IOnboardingSessionStore store,
        IBiometricService biometricService,
        IRiskScoringService riskService,
        IAuditService audit)
    {
        var error = Validation.Validate(request);
        if (error is not null) return error;

        var session = store.Get(id);
        if (session is null) return Results.NotFound();

        if (session.Status != VerificationStatus.DocumentSubmitted)
            return Results.Conflict(new { message = "Document verification must be completed successfully before submitting biometrics." });

        var bioResult = await biometricService.VerifyAsync(request);
        session.BiometricResult = bioResult;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        if (!bioResult.FaceMatchPassed || !bioResult.LivenessCheckPassed)
        {
            session.Status = VerificationStatus.Rejected;
            audit.Log(session.Id, "BIOMETRIC_REJECTED", session.CustomerEmail, session.Channel.ToString(),
                bioResult.RejectionReason);
            return Results.Ok(new BiometricSubmissionResult(bioResult, null));
        }

        session.Status = VerificationStatus.BiometricVerified;
        audit.Log(session.Id, "BIOMETRIC_VERIFIED", session.CustomerEmail, session.Channel.ToString(),
            $"FaceMatchScore={bioResult.FaceMatchScore:F4}, Liveness=Passed");

        // Automatically run risk assessment once biometrics pass
        var riskResult = await riskService.AssessAsync(session);
        session.RiskResult = riskResult;
        session.Status = VerificationStatus.RiskAssessed;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        if (!riskResult.SanctionsScreeningPassed || riskResult.RiskLevel == RiskLevel.Critical)
        {
            session.Status = VerificationStatus.Rejected;
            audit.Log(session.Id, "RISK_REJECTED", session.CustomerEmail, session.Channel.ToString(),
                $"RiskLevel={riskResult.RiskLevel}, Flags={string.Join(",", riskResult.Flags)}");
        }
        else if (riskResult.RiskLevel == RiskLevel.High)
        {
            session.Status = VerificationStatus.RequiresReview;
            audit.Log(session.Id, "RISK_REQUIRES_REVIEW", session.CustomerEmail, session.Channel.ToString(),
                $"RiskLevel={riskResult.RiskLevel}, RiskScore={riskResult.RiskScore}");
        }
        else
        {
            session.Status = VerificationStatus.Approved;
            audit.Log(session.Id, "SESSION_APPROVED", session.CustomerEmail, session.Channel.ToString(),
                $"RiskLevel={riskResult.RiskLevel}, RiskScore={riskResult.RiskScore}");
        }

        return Results.Ok(new BiometricSubmissionResult(bioResult, riskResult));
    }

    private static IResult GetRiskAssessment(Guid id, IOnboardingSessionStore store)
    {
        var session = store.Get(id);
        if (session is null) return Results.NotFound();
        if (session.RiskResult is null)
            return Results.NotFound(new { message = "Risk assessment has not yet been performed for this session." });
        return Results.Ok(session.RiskResult);
    }
}

public sealed record BiometricSubmissionResult(
    BiometricVerificationResult BiometricResult,
    RiskAssessmentResult? RiskResult);
