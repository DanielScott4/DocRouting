using MediatR;
using Org.BouncyCastle.X509;
using PadesSign.Application.Interfaces;
using PadesSign.Application.Workflows;
using PadesSign.Domain.Entities;

namespace PadesSign.Application.Commands;

public record FinalizeSigningCommand(
    Guid             SessionId,
    Guid             UserId,
    byte[]           Pkcs7SignatureBytes,
    X509Certificate[] Chain,
    string           IpAddress) : IRequest;

public class FinalizeSigningHandler : IRequestHandler<FinalizeSigningCommand>
{
    private readonly ISigningSessionStore  _sessions;
    private readonly IPadesSigningService  _pades;
    private readonly IEnvelopeRepository   _envelopes;
    private readonly WorkflowOrchestrator  _workflow;

    public FinalizeSigningHandler(ISigningSessionStore sessions, IPadesSigningService pades,
        IEnvelopeRepository envelopes, WorkflowOrchestrator workflow)
    { _sessions = sessions; _pades = pades; _envelopes = envelopes; _workflow = workflow; }

    public async Task Handle(FinalizeSigningCommand cmd, CancellationToken ct)
    {
        var session = await _sessions.GetAsync(cmd.SessionId, ct)
            ?? throw new InvalidOperationException("Signing session not found or expired.");
        if (session.IsExpired)   throw new InvalidOperationException("Signing session has expired.");
        if (session.UserId != cmd.UserId) throw new UnauthorizedAccessException();

        // Embed signature bytes + TSA timestamp -> PAdES-B-LT PDF
        var signedBlobPath = await _pades.FinalizeAsync(session, cmd.Pkcs7SignatureBytes, cmd.Chain, ct);

        // Record on the envelope domain object
        var envelope = await _envelopes.GetAsync(session.EnvelopeId, ct)!;
        var certSubject    = cmd.Chain[0].SubjectDN.ToString();
        var certThumbprint = Convert.ToHexString(cmd.Chain[0].GetSignature());
        var certSerial     = cmd.Chain[0].SerialNumber.ToString();

        var sig = new SignatureRecord
        {
            EnvelopeId            = session.EnvelopeId,
            SignedByUserId        = cmd.UserId,
            StepOrder             = session.StepOrder,
            CertificateSubject    = certSubject,
            CertificateThumbprint = certThumbprint,
            CertificateSerial     = certSerial,
            IpAddress             = cmd.IpAddress
        };
        envelope!.RecordSignature(sig, signedBlobPath);
        await _envelopes.UpdateAsync(envelope, ct);
        await _sessions.DeleteAsync(cmd.SessionId, ct);

        // Advance the workflow (notifies next signatories or marks complete)
        await _workflow.AdvanceAsync(session.EnvelopeId, session.StepOrder, ct);
    }
}