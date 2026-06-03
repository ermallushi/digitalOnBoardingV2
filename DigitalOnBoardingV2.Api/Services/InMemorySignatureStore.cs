using System.Collections.Concurrent;
using DigitalOnBoardingV2.Api.Models;

namespace DigitalOnBoardingV2.Api.Services;

public interface ISignatureStore
{
    void Add(SignatureRequest request);
    SignatureRequest? Get(Guid id);
}

public sealed class InMemorySignatureStore : ISignatureStore
{
    private readonly ConcurrentDictionary<Guid, SignatureRequest> _requests = new();

    public void Add(SignatureRequest request) => _requests[request.Id] = request;
    public SignatureRequest? Get(Guid id) => _requests.TryGetValue(id, out var r) ? r : null;
}
