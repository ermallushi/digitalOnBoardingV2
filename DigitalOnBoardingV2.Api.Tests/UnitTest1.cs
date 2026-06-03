using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DigitalOnBoardingV2.Api.Tests;

public class RfiApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RfiApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRfiDocument_ReturnsExpectedObjective()
    {
        var result = await _client.GetFromJsonAsync<RfiDocumentResponse>("/api/rfi");

        Assert.NotNull(result);
        Assert.Contains(result.Objectives, x => x.Contains("unified KYC framework", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.SolutionRequirements.DigitalSignatureCapabilities, x => x.Contains("qualified electronic signatures", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostVendorResponse_WithInvalidPayload_ReturnsBadRequest()
    {
        var payload = new
        {
            vendorName = "",
            contactEmail = "not-an-email",
            capabilities = Array.Empty<string>()
        };

        var response = await _client.PostAsJsonAsync("/api/rfi/vendor-responses", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostVendorResponse_WithValidPayload_CreatesSubmission()
    {
        var payload = new
        {
            vendorName = "Trusted KYC Vendor",
            contactEmail = "contact@trustedkyc.com",
            capabilities = new[]
            {
                "Document validation and OCR",
                "Liveness-based facial verification",
                "In-store kiosk verification"
            },
            complianceFrameworks = new[] { "GDPR", "AML", "eIDAS" },
            notes = "Supports cross-channel onboarding and digital signatures"
        };

        var response = await _client.PostAsJsonAsync("/api/rfi/vendor-responses", payload);
        var created = await response.Content.ReadFromJsonAsync<VendorSubmissionResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(payload.vendorName, created.VendorName);
        Assert.Contains("AML", created.ComplianceFrameworks);
    }

    private sealed record RfiDocumentResponse(
        IReadOnlyList<string> Objectives,
        SolutionRequirementsResponse SolutionRequirements);

    private sealed record SolutionRequirementsResponse(
        IReadOnlyList<string> DigitalSignatureCapabilities);

    private sealed record VendorSubmissionResponse(
        Guid Id,
        string VendorName,
        IReadOnlyList<string> ComplianceFrameworks);
}
