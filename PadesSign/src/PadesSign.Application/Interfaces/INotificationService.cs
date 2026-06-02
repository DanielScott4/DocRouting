using PadesSign.Domain.Entities;

namespace PadesSign.Application.Interfaces;

public interface INotificationService
{
    Task NotifySignatoryAsync(DocumentEnvelope envelope, WorkflowStep step, CancellationToken ct = default);
    Task NotifyCompletedAsync(DocumentEnvelope envelope, CancellationToken ct = default);
    Task NotifyDeclinedAsync(DocumentEnvelope envelope, Guid declinedByUserId, CancellationToken ct = default);
}