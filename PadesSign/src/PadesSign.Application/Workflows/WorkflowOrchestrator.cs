using PadesSign.Application.Interfaces;
using PadesSign.Domain.Entities;
using PadesSign.Domain.Enums;

namespace PadesSign.Application.Workflows;

public class WorkflowOrchestrator
{
    private readonly IEnvelopeRepository  _envelopes;
    private readonly ITemplateRepository  _templates;
    private readonly INotificationService _notifications;

    public WorkflowOrchestrator(
        IEnvelopeRepository  envelopes,
        ITemplateRepository  templates,
        INotificationService notifications)
    {
        _envelopes     = envelopes;
        _templates     = templates;
        _notifications = notifications;
    }

    public async Task StartAsync(Guid envelopeId, CancellationToken ct = default)
    {
        var envelope = await GetEnvelopeAsync(envelopeId, ct);
        var template = await GetTemplateAsync(envelope.TemplateId, ct);
        var firstOrder = template.Steps.Min(s => s.Order);
        envelope.Start(firstOrder);
        await _envelopes.UpdateAsync(envelope, ct);
        await NotifyCurrentStepAsync(envelope, template, ct);
    }

    /// <summary>Called after a step is successfully signed.</summary>
    public async Task AdvanceAsync(Guid envelopeId, int completedStepOrder, CancellationToken ct = default)
    {
        var envelope = await GetEnvelopeAsync(envelopeId, ct);
        var template = await GetTemplateAsync(envelope.TemplateId, ct);

        // Are there still unsigned parallel sibling steps at the current order?
        var siblingSigned = envelope.Signatures.Count(s => s.StepOrder == completedStepOrder);
        var siblingTotal  = template.Steps.Count(s => s.Order == completedStepOrder);
        if (siblingSigned < siblingTotal) return; // wait for remaining siblings

        // Find next step order
        var nextOrder = template.Steps
            .Where(s => s.Order > completedStepOrder)
            .Select(s => (int?)s.Order)
            .Min();

        if (nextOrder is null)
        {
            envelope.Complete();
            await _envelopes.UpdateAsync(envelope, ct);
            await _notifications.NotifyCompletedAsync(envelope, ct);
            return;
        }

        envelope.AdvanceToStep(nextOrder.Value);
        await _envelopes.UpdateAsync(envelope, ct);
        await NotifyCurrentStepAsync(envelope, template, ct);
    }

    public async Task DeclineAsync(Guid envelopeId, Guid userId, CancellationToken ct = default)
    {
        var envelope = await GetEnvelopeAsync(envelopeId, ct);
        envelope.Decline();
        await _envelopes.UpdateAsync(envelope, ct);
        await _notifications.NotifyDeclinedAsync(envelope, userId, ct);
    }

    private async Task NotifyCurrentStepAsync(DocumentEnvelope envelope, WorkflowTemplate template, CancellationToken ct)
    {
        var steps = template.Steps.Where(s => s.Order == envelope.CurrentStepOrder);
        foreach (var step in steps)
            await _notifications.NotifySignatoryAsync(envelope, step, ct);
    }

    private async Task<DocumentEnvelope> GetEnvelopeAsync(Guid id, CancellationToken ct)
        => await _envelopes.GetAsync(id, ct)
           ?? throw new InvalidOperationException($"Envelope {id} not found.");

    private async Task<WorkflowTemplate> GetTemplateAsync(Guid id, CancellationToken ct)
        => await _templates.GetAsync(id, ct)
           ?? throw new InvalidOperationException($"Template {id} not found.");
}