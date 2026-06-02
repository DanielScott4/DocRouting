using Microsoft.EntityFrameworkCore;
using PadesSign.Application.Interfaces;
using PadesSign.Domain.Entities;

namespace PadesSign.Infrastructure.Data.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly PadesSignDbContext _db;
    public TemplateRepository(PadesSignDbContext db) => _db = db;

    public async Task<WorkflowTemplate?> GetAsync(Guid id, CancellationToken ct)
        => await _db.WorkflowTemplates.Include(t => t.Steps)
               .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<WorkflowTemplate>> ListAsync(CancellationToken ct)
        => await _db.WorkflowTemplates.Include(t => t.Steps)
               .Where(t => !t.IsArchived)
               .OrderBy(t => t.Name).ToListAsync(ct);

    public async Task AddAsync(WorkflowTemplate template, CancellationToken ct)
    { _db.WorkflowTemplates.Add(template); await _db.SaveChangesAsync(ct); }

    public async Task UpdateAsync(WorkflowTemplate template, CancellationToken ct)
    { _db.WorkflowTemplates.Update(template); await _db.SaveChangesAsync(ct); }
}