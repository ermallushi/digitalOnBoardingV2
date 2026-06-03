namespace DigitalOnBoardingV2.Api.Models;

public enum OnboardingChannel { Digital, InStore }

public enum VerificationStatus
{
    Pending,
    DocumentSubmitted,
    BiometricVerified,
    RiskAssessed,
    Approved,
    Rejected,
    RequiresReview
}

public enum DocumentType { Passport, DriversLicense, NationalId }

public enum RiskLevel { Low, Medium, High, Critical }

public enum SignatureLevel { Simple, Advanced, Qualified }

public enum SignatureStatus { Pending, PartiallySigned, Completed, Expired, Revoked }
