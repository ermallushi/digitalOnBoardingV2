using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var vendorResponses = new ConcurrentQueue<VendorResponseSubmission>();
var submissionCounter = 0;

app.MapGet("/api/rfi", () => Results.Ok(RfiDocument.CreateDefault()))
    .WithName("GetRfiDocument");

app.MapGet("/api/rfi/vendor-responses", () => Results.Ok(vendorResponses.ToArray()))
    .WithName("GetVendorResponses");

app.MapPost("/api/rfi/vendor-responses", (VendorResponseRequest request) =>
{
    var validationContext = new ValidationContext(request);
    var validationResults = new List<ValidationResult>();

    if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true) ||
        request.Capabilities.Count == 0)
    {
        return Results.ValidationProblem(validationResults
            .GroupBy(x => x.MemberNames.FirstOrDefault() ?? "request")
            .ToDictionary(x => x.Key, x => x.Select(r => r.ErrorMessage ?? "Invalid value").ToArray()));
    }

    var submission = new VendorResponseSubmission(
        Interlocked.Increment(ref submissionCounter),
        request.VendorName.Trim(),
        request.ContactEmail.Trim(),
        request.Capabilities,
        request.ComplianceFrameworks,
        request.Notes,
        DateTimeOffset.UtcNow);

    vendorResponses.Enqueue(submission);

    return Results.Created($"/api/rfi/vendor-responses/{submission.Id}", submission);
})
.WithName("SubmitVendorResponse");

app.Run();

public record RfiDocument(
    string Introduction,
    IReadOnlyList<string> Purpose,
    IReadOnlyList<string> Objectives,
    ScopeOfWork Scope,
    SolutionRequirements SolutionRequirements,
    IntegrationRequirements IntegrationRequirements)
{
    public static RfiDocument CreateDefault() => new(
        "One Albania seeks qualified KYC solutions for digital and in-store onboarding, including compliant digital signatures.",
        [
            "Gather information on available KYC solutions and technologies.",
            "Understand vendor capabilities in digital onboarding and in-store KYC.",
            "Assess feasibility of integrating digital signature technologies.",
            "Assess suitability of vendor solutions for One Albania requirements.",
            "Identify potential vendors for future RFP or POC stages."
        ],
        [
            "Implement a unified KYC framework across digital and physical channels.",
            "Reduce onboarding time while maintaining or improving verification quality.",
            "Meet regulatory requirements across operating jurisdictions.",
            "Enable secure digital signature capabilities for document execution.",
            "Create an adaptable system that can evolve with changing regulations and business needs."
        ],
        new ScopeOfWork(
            [
                "Identity verification with document scanning and biometric authentication.",
                "KYC compliance for GDPR, AML, and related frameworks.",
                "Integration with digital channels, CRM, and ERP systems."
            ],
            [
                "KYC workflows for in-store customer verification.",
                "Digital signature integration for in-store paperwork."
            ],
            [
                "E-signature technologies such as PKI and blockchain.",
                "Compliance with legal frameworks including eIDAS and UETA.",
                "Security controls to protect sensitive data."
            ]),
        new SolutionRequirements(
            [
                "Document validation for passports, driver's licenses, and national ID cards.",
                "OCR and NFC support for automated extraction.",
                "Forgery and tamper detection.",
                "Multi-country/multi-jurisdiction document support."
            ],
            [
                "Selfie-to-ID facial matching.",
                "Liveness detection against spoofing.",
                "Voice recognition support where available.",
                "Fingerprint integration with mobile devices where available."
            ],
            [
                "Real-time screening against sanctions, PEP, and adverse media.",
                "Fraud detection algorithms.",
                "Configurable risk scoring.",
                "Case management for exceptions and escalations."
            ],
            [
                "Physical-location identity checks using mobile devices or kiosks.",
                "In-store biometric capture.",
                "In-store document scanning.",
                "Instant verification results."
            ],
            [
                "User-friendly interfaces for store staff.",
                "Training resources and onboarding enablement.",
                "Role-based access controls.",
                "Audit trail of staff actions."
            ],
            [
                "Start verification in one channel and complete in another.",
                "Consistent customer experience across channels.",
                "Data synchronization between digital and physical channels."
            ],
            [
                "Support simple, advanced, and qualified electronic signatures.",
                "Tamper-evident document sealing.",
                "Mobile and in-person signing experiences.",
                "Multi-party workflows, status tracking, and notifications.",
                "Comprehensive audit trails and long-term validation."
            ]),
        new IntegrationRequirements(
            [
                "APIs and plug-and-play connectors for external systems and partners.",
                "Integration with existing business support systems.",
                "Single sign-on support.",
                "Data migration support."
            ],
            [
                "Encryption in transit and at rest.",
                "Access controls and authentication.",
                "Security certifications such as ISO 27001 and SOC 2.",
                "Penetration testing and vulnerability management."
            ],
            [
                "Concurrent user handling and high throughput.",
                "Defined transaction processing times.",
                "Uptime guarantees.",
                "Disaster recovery capabilities."
            ]));
}

public record ScopeOfWork(
    IReadOnlyList<string> DigitalOnboarding,
    IReadOnlyList<string> PhysicalShops,
    IReadOnlyList<string> DigitalSignature);

public record SolutionRequirements(
    IReadOnlyList<string> IdentityVerification,
    IReadOnlyList<string> BiometricVerification,
    IReadOnlyList<string> RiskAssessment,
    IReadOnlyList<string> InStoreVerification,
    IReadOnlyList<string> StaffInterfaces,
    IReadOnlyList<string> CrossChannelCoordination,
    IReadOnlyList<string> DigitalSignatureCapabilities);

public record IntegrationRequirements(
    IReadOnlyList<string> SystemIntegration,
    IReadOnlyList<string> SecurityRequirements,
    IReadOnlyList<string> ScalabilityAndPerformance);

public record VendorResponseSubmission(
    int Id,
    string VendorName,
    string ContactEmail,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> ComplianceFrameworks,
    string? Notes,
    DateTimeOffset SubmittedAt);

public class VendorResponseRequest
{
    [Required]
    [MaxLength(200)]
    public string VendorName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(250)]
    public string ContactEmail { get; init; } = string.Empty;

    [MinLength(1)]
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    public IReadOnlyList<string> ComplianceFrameworks { get; init; } = [];

    [MaxLength(2000)]
    public string? Notes { get; init; }
}

public partial class Program;
