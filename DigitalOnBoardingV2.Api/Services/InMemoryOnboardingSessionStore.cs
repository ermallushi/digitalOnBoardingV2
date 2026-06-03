using System.Collections.Concurrent;
using DigitalOnBoardingV2.Api.Models;

namespace DigitalOnBoardingV2.Api.Services;

public interface IOnboardingSessionStore
{
    void Add(OnboardingSession session);
    OnboardingSession? Get(Guid id);
}

public sealed class InMemoryOnboardingSessionStore : IOnboardingSessionStore
{
    private readonly ConcurrentDictionary<Guid, OnboardingSession> _sessions = new();

    public void Add(OnboardingSession session) => _sessions[session.Id] = session;
    public OnboardingSession? Get(Guid id) => _sessions.TryGetValue(id, out var s) ? s : null;
}
