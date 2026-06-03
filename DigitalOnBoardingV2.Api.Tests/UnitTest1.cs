using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DigitalOnBoardingV2.Api.Tests;

public class KycOnboardingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public KycOnboardingTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static string FakeBase64() => Convert.ToBase64String(new byte[100]);
    private static string FutureExpiry() =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5)).ToString("yyyy-MM-dd");
    private static string PastExpiry() =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)).ToString("yyyy-MM-dd");

    // ── Digital onboarding ──────────────────────────────────────────────────

    [Fact]
    public async Task StartOnboarding_ValidRequest_CreatesSession()
    {
        var response = await _client.PostAsJsonAsync("/api/onboarding/sessions", new
        {
            customerId = Guid.NewGuid(),
            fullName = "Alice Smith",
            email = "alice@example.com",
            channel = 0 // Digital
        });

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(session);
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal("Alice Smith", session.CustomerFullName);
        Assert.Equal(0, session.Status); // Pending
    }

    [Fact]
    public async Task StartOnboarding_InvalidEmail_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/onboarding/sessions", new
        {
            customerId = Guid.NewGuid(),
            fullName = "Bob",
            email = "not-valid-email",
            channel = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitDocument_ValidNonExpired_AdvancesSessionToDocumentSubmitted()
    {
        var sessionId = await CreateSession("Charlie Brown", "charlie@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/onboarding/sessions/{sessionId}/document",
            new
            {
                documentType = 0, // Passport
                documentNumber = "P1234567",
                issuingCountry = "AL",
                expiryDate = FutureExpiry(),
                documentImageBase64 = FakeBase64()
            });

        var result = await response.Content.ReadFromJsonAsync<DocResultResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result.IsAuthentic);
        Assert.False(result.IsTampered);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public async Task SubmitDocument_ExpiredDocument_RejectsSession()
    {
        var sessionId = await CreateSession("Dave Expired", "dave@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/onboarding/sessions/{sessionId}/document",
            new
            {
                documentType = 1, // DriversLicense
                documentNumber = "D9999999",
                issuingCountry = "AL",
                expiryDate = PastExpiry(),
                documentImageBase64 = FakeBase64()
            });

        var result = await response.Content.ReadFromJsonAsync<DocResultResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.IsAuthentic);
        Assert.NotNull(result.RejectionReason);
    }

    [Fact]
    public async Task FullDigitalOnboarding_LowRiskCustomer_GetsApproved()
    {
        var sessionId = await CreateSession("Eve Happy", "eve@example.com");
        await SubmitValidDocument(sessionId);

        var bioResponse = await _client.PostAsJsonAsync(
            $"/api/onboarding/sessions/{sessionId}/biometric",
            new { selfieImageBase64 = FakeBase64(), fingerprintProvided = false });

        Assert.Equal(HttpStatusCode.OK, bioResponse.StatusCode);

        var session = await _client.GetFromJsonAsync<SessionResponse>(
            $"/api/onboarding/sessions/{sessionId}");

        Assert.NotNull(session);
        Assert.Equal(4, session.Status); // Approved
    }

    [Fact]
    public async Task GetSession_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/onboarding/sessions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubmitBiometric_BeforeDocument_ReturnsConflict()
    {
        var sessionId = await CreateSession("Frank No-Doc", "frank@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/onboarding/sessions/{sessionId}/biometric",
            new { selfieImageBase64 = FakeBase64(), fingerprintProvided = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── In-store KYC ────────────────────────────────────────────────────────

    [Fact]
    public async Task InstoreScan_ValidCustomer_IsApproved()
    {
        var sessionResp = await _client.PostAsJsonAsync("/api/instore/sessions", new
        {
            customerId = Guid.NewGuid(),
            fullName = "Grace Instore",
            email = "grace@example.com",
            staffId = "STAFF001",
            locationCode = "TIR-01"
        });

        var session = await sessionResp.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.Equal(HttpStatusCode.Created, sessionResp.StatusCode);
        Assert.NotNull(session);

        var scanResp = await _client.PostAsJsonAsync(
            $"/api/instore/sessions/{session.Id}/scan",
            new
            {
                documentType = 2, // NationalId
                documentNumber = "N1234567",
                issuingCountry = "AL",
                expiryDate = FutureExpiry(),
                documentImageBase64 = FakeBase64(),
                selfieImageBase64 = FakeBase64()
            });

        var result = await scanResp.Content.ReadFromJsonAsync<InstoreResultResponse>();

        Assert.Equal(HttpStatusCode.OK, scanResp.StatusCode);
        Assert.NotNull(result);
        Assert.True(result.Approved);
    }

    [Fact]
    public async Task InstoreScan_ExpiredDocument_IsRejected()
    {
        var sessionResp = await _client.PostAsJsonAsync("/api/instore/sessions", new
        {
            customerId = Guid.NewGuid(),
            fullName = "Henry Expired",
            email = "henry@example.com",
            staffId = "STAFF002",
            locationCode = "TIR-01"
        });

        var session = await sessionResp.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);

        var scanResp = await _client.PostAsJsonAsync(
            $"/api/instore/sessions/{session.Id}/scan",
            new
            {
                documentType = 0,
                documentNumber = "P0000000",
                issuingCountry = "AL",
                expiryDate = PastExpiry(),
                documentImageBase64 = FakeBase64(),
                selfieImageBase64 = FakeBase64()
            });

        var result = await scanResp.Content.ReadFromJsonAsync<InstoreResultResponse>();

        Assert.Equal(HttpStatusCode.OK, scanResp.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Approved);
        Assert.NotNull(result.RejectionReason);
    }

    // ── Digital signature ────────────────────────────────────────────────────

    [Fact]
    public async Task DigitalSignature_TwoSignatories_CompletesWithSeal()
    {
        var sessionId = await CreateApprovedSession("Iris Signer", "iris@example.com");
        const string docContent = "One Albania Customer Agreement – Contract v1.0";

        var createResp = await _client.PostAsJsonAsync("/api/signatures", new
        {
            sessionId,
            documentName = "Customer Agreement",
            documentContent = docContent,
            level = 1, // Advanced
            signatories = new[] { "iris@example.com", "officer@onealb.com" },
            expiryDays = 7
        });

        var sigReq = await createResp.Content.ReadFromJsonAsync<SigRequestResponse>();
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        Assert.NotNull(sigReq);

        // First signatory signs
        var sign1 = await _client.PostAsJsonAsync($"/api/signatures/{sigReq.Id}/sign",
            new { signatory = "iris@example.com", documentContent = docContent });
        Assert.Equal(HttpStatusCode.OK, sign1.StatusCode);

        // Second signatory signs
        var sign2 = await _client.PostAsJsonAsync($"/api/signatures/{sigReq.Id}/sign",
            new { signatory = "officer@onealb.com", documentContent = docContent });
        Assert.Equal(HttpStatusCode.OK, sign2.StatusCode);

        // Verify completion and tamper-evident seal
        var final = await _client.GetFromJsonAsync<SigRequestResponse>($"/api/signatures/{sigReq.Id}");
        Assert.NotNull(final);
        Assert.Equal(2, final.Status); // Completed
        Assert.NotNull(final.DocumentSeal);
    }

    [Fact]
    public async Task SignDocument_TamperedContent_ReturnsConflict()
    {
        var sessionId = await CreateApprovedSession("Jack Tamper", "jack@example.com");
        const string originalContent = "Original contract content.";
        const string tamperedContent = "Tampered contract content!";

        var createResp = await _client.PostAsJsonAsync("/api/signatures", new
        {
            sessionId,
            documentName = "Agreement",
            documentContent = originalContent,
            level = 1,
            signatories = new[] { "jack@example.com" },
            expiryDays = 7
        });

        var sigReq = await createResp.Content.ReadFromJsonAsync<SigRequestResponse>();
        Assert.NotNull(sigReq);

        // Attempt to sign with different (tampered) content
        var signResp = await _client.PostAsJsonAsync($"/api/signatures/{sigReq.Id}/sign",
            new { signatory = "jack@example.com", documentContent = tamperedContent });

        Assert.Equal(HttpStatusCode.Conflict, signResp.StatusCode);
    }

    [Fact]
    public async Task SignDocument_UnauthorizedSignatory_Returns403()
    {
        var sessionId = await CreateApprovedSession("Karen Auth", "karen@example.com");
        const string docContent = "Authorized signatories only.";

        var createResp = await _client.PostAsJsonAsync("/api/signatures", new
        {
            sessionId,
            documentName = "Restricted Doc",
            documentContent = docContent,
            level = 0,
            signatories = new[] { "karen@example.com" },
            expiryDays = 7
        });

        var sigReq = await createResp.Content.ReadFromJsonAsync<SigRequestResponse>();
        Assert.NotNull(sigReq);

        var signResp = await _client.PostAsJsonAsync($"/api/signatures/{sigReq.Id}/sign",
            new { signatory = "intruder@example.com", documentContent = docContent });

        Assert.Equal(HttpStatusCode.Forbidden, signResp.StatusCode);
    }

    // ── Audit trail ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AuditTrail_AfterFullOnboarding_ContainsAllKeyEvents()
    {
        var sessionId = await CreateApprovedSession("Leo Audit", "leo@example.com");

        var entries = await _client.GetFromJsonAsync<AuditEntryResponse[]>($"/api/audit/{sessionId}");

        Assert.NotNull(entries);
        Assert.Contains(entries, e => e.Action == "SESSION_STARTED");
        Assert.Contains(entries, e => e.Action == "DOCUMENT_VERIFIED");
        Assert.Contains(entries, e => e.Action == "BIOMETRIC_VERIFIED");
        Assert.Contains(entries, e => e.Action == "SESSION_APPROVED");
    }

    [Fact]
    public async Task AuditTrail_NonExistentSession_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/audit/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> CreateSession(string fullName, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/onboarding/sessions",
            new { customerId = Guid.NewGuid(), fullName, email, channel = 0 });
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        return session!.Id;
    }

    private async Task SubmitValidDocument(Guid sessionId)
    {
        await _client.PostAsJsonAsync($"/api/onboarding/sessions/{sessionId}/document", new
        {
            documentType = 0,
            documentNumber = "P1111111",
            issuingCountry = "AL",
            expiryDate = FutureExpiry(),
            documentImageBase64 = FakeBase64()
        });
    }

    private async Task<Guid> CreateApprovedSession(string fullName, string email)
    {
        var sessionId = await CreateSession(fullName, email);
        await SubmitValidDocument(sessionId);
        await _client.PostAsJsonAsync($"/api/onboarding/sessions/{sessionId}/biometric",
            new { selfieImageBase64 = FakeBase64(), fingerprintProvided = false });
        return sessionId;
    }

    // ── response DTOs ────────────────────────────────────────────────────────
    private sealed record SessionResponse(Guid Id, string CustomerFullName, int Status);
    private sealed record DocResultResponse(bool IsAuthentic, bool IsTampered, string? RejectionReason);
    private sealed record InstoreResultResponse(bool Approved, int Status, string? RejectionReason);
    private sealed record SigRequestResponse(Guid Id, int Status, string? DocumentSeal);
    private sealed record AuditEntryResponse(Guid Id, Guid SessionId, string Action, string PerformedBy);
}

