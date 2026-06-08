using System.Collections.Concurrent;
using PadesSign.Application.Interfaces;
using PadesSign.Domain.Entities;

namespace PadesSign.Infrastructure.Storage;

/// <summary>
/// In-process store. Replace with Redis IDistributedCache for multi-node deployments.
/// </summary>
public class InMemorySigningSessionStore : ISigningSessionStore
{
    private readonly ConcurrentDictionary<Guid, SigningSession> _store = new();

    public Task SaveAsync(SigningSession s, CancellationToken ct)
    { _store[s.Id] = s; return Task.CompletedTask; }

    public Task<SigningSession?> GetAsync(Guid id, CancellationToken ct)
    { _store.TryGetValue(id, out var s); return Task.FromResult(s); }

    public Task DeleteAsync(Guid id, CancellationToken ct)
    { _store.TryRemove(id, out _); return Task.CompletedTask; }
}