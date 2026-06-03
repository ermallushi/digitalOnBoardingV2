using DigitalOnBoardingV2.Api.Models;

namespace DigitalOnBoardingV2.Api.Services;

public interface IBiometricService
{
    Task<BiometricVerificationResult> VerifyAsync(SubmitBiometricRequest request);
}

/// <summary>
/// Simulates facial recognition, liveness detection, and fingerprint verification.
/// In production: integrates with a biometric vendor API (e.g., iProov, FaceTec, BioID).
/// </summary>
public sealed class BiometricService : IBiometricService
{
    private const double FaceMatchThreshold = 0.80;

    public Task<BiometricVerificationResult> VerifyAsync(SubmitBiometricRequest request)
    {
        // Simulate: derive a deterministic-ish score from the image payload
        var score = 0.87 + (request.SelfieImageBase64.Length % 10) * 0.01;
        score = Math.Min(score, 0.99);

        var faceMatchPassed = score >= FaceMatchThreshold;
        // Simulate liveness: non-empty selfie image is treated as a live capture
        var livenessCheckPassed = request.SelfieImageBase64.Length > 0;
        var fingerprintVerified = request.FingerprintProvided;

        string? rejectionReason = null;
        if (!faceMatchPassed)
            rejectionReason = "Face match score is below the required threshold.";
        else if (!livenessCheckPassed)
            rejectionReason = "Liveness check failed — possible spoofing attempt detected.";

        return Task.FromResult(new BiometricVerificationResult(
            faceMatchPassed,
            Math.Round(score, 4),
            livenessCheckPassed,
            fingerprintVerified,
            rejectionReason,
            DateTimeOffset.UtcNow));
    }
}
