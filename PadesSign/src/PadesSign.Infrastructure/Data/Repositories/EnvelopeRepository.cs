using Microsoft.EntityFrameworkCore;
using PadesSign.Application.Interfaces;
using PadesSign.Domain.Entities;
using PadesSign.Domain.Enums;

namespace PadesSign.Infrastructure.Data.Repositories;

public class EnvelopeRepository : IEnvelopeRepository
{
    private readonly PadesSignDbContext _db;
    public EnvelopeRepository(PadesSignDbContext db) => _db = db;

    public async Task<DocumentEnvelope?> GetAsync(Guid id, CancellationToken ct)
        => await _db.Envelopes.Include(e => e.Signatures)
               .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<DocumentEnvelope>> ListByUserAsync(Guid userId, CancellationToken ct)
        => await _db.Envelopes.Where(e => e.UploadedByUserId == userId)
               .OrderByDescending(e => e.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentEnvelope>> ListPendingForUserAsync(Guid userId, CancellationToken ct)
        => await _db.Envelopes.Include(e => e.Signatures)
               .Where(e => e.Status == EnvelopeStatus.InProgress)
               .ToListAsync(ct);

    public async Task AddAsync(DocumentEnvelope envelope, CancellationToken ct)
    { _db.Envelopes.Add(envelope); await _db.SaveChangesAsync(ct); }

    public async Task UpdateAsync(DocumentEnvelope envelope, CancellationToken ct)
    { _db.Envelopes.Update(envelope); await _db.SaveChangesAsync(ct); }
}