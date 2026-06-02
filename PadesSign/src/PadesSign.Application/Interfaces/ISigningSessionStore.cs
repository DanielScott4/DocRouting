using PadesSign.Domain.Entities;

namespace PadesSign.Application.Interfaces;

/// <summary>
/// Short-lived in-memory (or Redis) store for signing sessions.
/// Sessions expire after 10 minutes.
/// </summary>
public interface ISigningSessionStore
{
    Task SaveAsync(SigningSession session, CancellationToken ct = default);
    Task<SigningSession?> GetAsync(Guid sessionId, CancellationToken ct = default);
    Task DeleteAsync(Guid sessionId, CancellationToken ct = default);
}