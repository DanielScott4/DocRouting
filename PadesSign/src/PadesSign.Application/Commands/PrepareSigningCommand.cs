using MediatR;
using Org.BouncyCastle.X509;
using PadesSign.Application.Interfaces;
using PadesSign.Domain.Entities;

namespace PadesSign.Application.Commands;

public record PrepareSigningCommand(
    Guid            EnvelopeId,
    Guid            UserId,
    X509Certificate SignerCertificate) : IRequest<SigningSession>;

public class PrepareSigningHandler : IRequestHandler<PrepareSigningCommand, SigningSession>
{
    private readonly IEnvelopeRepository  _envelopes;
    private readonly ITemplateRepository  _templates;
    private readonly IPadesSigningService _pades;
    private readonly ISigningSessionStore _sessions;

    public PrepareSigningHandler(IEnvelopeRepository envelopes, ITemplateRepository templates,
        IPadesSigningService pades, ISigningSessionStore sessions)
    { _envelopes = envelopes; _templates = templates; _pades = pades; _sessions = sessions; }

    public async Task<SigningSession> Handle(PrepareSigningCommand cmd, CancellationToken ct)
    {
        var envelope = await _envelopes.GetAsync(cmd.EnvelopeId, ct)
            ?? throw new InvalidOperationException("Envelope not found.");
        var template = await _templates.GetAsync(envelope.TemplateId, ct)
            ?? throw new InvalidOperationException("Template not found.");

        // Find the step assigned to this user at the current order
        var step = template.Steps.FirstOrDefault(s =>
            s.Order == envelope.CurrentStepOrder &&
            (s.AssigneeId == cmd.UserId || /* role check would go here */ false))
            ?? throw new UnauthorizedAccessException("No pending step for this user.");

        var session = await _pades.PrepareAsync(
            cmd.EnvelopeId, cmd.UserId, step.Order,
            envelope.WorkingBlobPath, step.Field, cmd.SignerCertificate, ct);

        await _sessions.SaveAsync(session, ct);
        return session;
    }
}