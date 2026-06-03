namespace DigitalOnBoardingV2.Api.Models;

public sealed record AuditEntry(
    Guid Id,
    Guid SessionId,
    string Action,
    string PerformedBy,
    string Channel,
    string? Details,
    DateTimeOffset Timestamp);
