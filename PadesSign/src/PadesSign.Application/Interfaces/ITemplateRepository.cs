using PadesSign.Domain.Entities;

namespace PadesSign.Application.Interfaces;

public interface ITemplateRepository
{
    Task<WorkflowTemplate?>            GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTemplate>> ListAsync(CancellationToken ct = default);
    Task                               AddAsync(WorkflowTemplate template, CancellationToken ct = default);
    Task                               UpdateAsync(WorkflowTemplate template, CancellationToken ct = default);
}