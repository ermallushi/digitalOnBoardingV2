using System.Collections.Concurrent;
using DigitalOnBoardingV2.Api.Models;

namespace DigitalOnBoardingV2.Api.Services;

public interface IAuditService
{
    void Log(Guid sessionId, string action, string performedBy, string channel, string? details = null);
    IReadOnlyList<AuditEntry> GetEntries(Guid sessionId);
}

/// <summary>
/// Thread-safe in-memory audit service. Maintains a full audit trail of all KYC verification actions.
/// In production: persisted to an immutable append-only store (e.g., event sourcing or a WORM database).
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<AuditEntry>> _entries = new();

    public void Log(Guid sessionId, string action, string performedBy, string channel, string? details = null)
    {
        var queue = _entries.GetOrAdd(sessionId, _ => new ConcurrentQueue<AuditEntry>());
        queue.Enqueue(new AuditEntry(Guid.NewGuid(), sessionId, action, performedBy, channel, details, DateTimeOffset.UtcNow));
    }

    public IReadOnlyList<AuditEntry> GetEntries(Guid sessionId) =>
        _entries.TryGetValue(sessionId, out var queue) ? queue.ToArray() : [];
}
