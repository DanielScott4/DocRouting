using PadesSign.Domain.Entities;
using PadesSign.Domain.Enums;

namespace PadesSign.Application.Interfaces;

public interface IEnvelopeRepository
{
    Task<DocumentEnvelope?>            GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentEnvelope>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentEnvelope>> ListPendingForUserAsync(Guid userId, CancellationToken ct = default);
    Task                               AddAsync(DocumentEnvelope envelope, CancellationToken ct = default);
    Task                               UpdateAsync(DocumentEnvelope envelope, CancellationToken ct = default);
}