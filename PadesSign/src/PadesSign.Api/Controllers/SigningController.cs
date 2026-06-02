using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Org.BouncyCastle.X509;
using PadesSign.Api.Hubs;
using PadesSign.Application.Commands;
using PadesSign.Application.Interfaces;

namespace PadesSign.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SigningController : ControllerBase
{
    private readonly IMediator              _mediator;
    private readonly ISigningSessionStore   _sessions;
    private readonly IHubContext<SigningHub> _hub;

    public SigningController(IMediator mediator, ISigningSessionStore sessions,
        IHubContext<SigningHub> hub)
    { _mediator = mediator; _sessions = sessions; _hub = hub; }

    /// <summary>
    /// Phase 1: client POSTs the DER-encoded public certificate from the smartcard.
    /// Returns the SHA-256 digest the smartcard must sign.
    /// </summary>
    [HttpPost("{envelopeId:guid}/prepare")]
    public async Task<IActionResult> Prepare(
        Guid envelopeId, [FromBody] PrepareRequest req, CancellationToken ct)
    {
        var certBytes = Convert.FromBase64String(req.CertificateBase64);
        var cert      = new X509CertificateParser().ReadCertificate(certBytes);

        var session = await _mediator.Send(
            new PrepareSigningCommand(envelopeId, GetUserId(), cert), ct);

        return Ok(new
        {
            session.Id,
            session.DigestBase64,
            session.DigestAlgorithm
        });
    }

    /// <summary>
    /// Phase 2: client POSTs the raw PKCS#7 / CMS bytes produced by the smartcard.
    /// Server embeds them, stamps TSA timestamp, writes PAdES-B-LT PDF.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/finalize")]
    public async Task<IActionResult> Finalize(
        Guid sessionId, [FromBody] FinalizeRequest req, CancellationToken ct)
    {
        var sigBytes = Convert.FromBase64String(req.Pkcs7SignatureBase64);
        var parser   = new X509CertificateParser();
        var chain    = req.ChainBase64
            .Select(b => parser.ReadCertificate(Convert.FromBase64String(b)))
            .ToArray();

        var session = await _sessions.GetAsync(sessionId, ct);
        if (session is null) return NotFound("Session not found.");

        await _mediator.Send(new FinalizeSigningCommand(
            sessionId, GetUserId(), sigBytes, chain,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""), ct);

        // Notify all watchers of this envelope
        await _hub.Clients.Group($"envelope:{session.EnvelopeId}")
            .SendAsync("StepSigned", new { session.StepOrder, signedAt = DateTime.UtcNow }, ct);

        return Ok();
    }

    private Guid GetUserId()
        => Guid.Parse(User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID claim missing."));
}

public record PrepareRequest(string CertificateBase64);
public record FinalizeRequest(string Pkcs7SignatureBase64, string[] ChainBase64);