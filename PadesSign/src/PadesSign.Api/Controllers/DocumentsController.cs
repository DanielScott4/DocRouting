using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PadesSign.Api.Hubs;
using PadesSign.Application.Commands;
using PadesSign.Application.Interfaces;
using PadesSign.Application.Workflows;

namespace PadesSign.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IMediator            _mediator;
    private readonly IEnvelopeRepository  _envelopes;
    private readonly IBlobStorage         _blobs;
    private readonly WorkflowOrchestrator _workflow;
    private readonly IHubContext<SigningHub> _hub;

    public DocumentsController(IMediator mediator, IEnvelopeRepository envelopes,
        IBlobStorage blobs, WorkflowOrchestrator workflow, IHubContext<SigningHub> hub)
    {
        _mediator  = mediator; _envelopes = envelopes;
        _blobs     = blobs;    _workflow  = workflow; _hub = hub;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(52_428_800)] // 50 MB
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file, [FromForm] Guid templateId, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var envelopeId = await _mediator.Send(
            new UploadDocumentCommand(templateId, GetUserId(), file.FileName, ms.ToArray()), ct);

        // Kick off the first workflow step
        await _workflow.StartAsync(envelopeId, ct);

        return Ok(new { envelopeId });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var envelope = await _envelopes.GetAsync(id, ct);
        if (envelope is null) return NotFound();
        return Ok(envelope);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var envelope = await _envelopes.GetAsync(id, ct);
        if (envelope is null) return NotFound();

        var url = await _blobs.GetDownloadUrlAsync(envelope.WorkingBlobPath,
            TimeSpan.FromMinutes(5), ct);
        return Redirect(url);
    }

    [HttpGet("{id:guid}/pdf-stream")]
    public async Task<IActionResult> PdfStream(Guid id, CancellationToken ct)
    {
        var envelope = await _envelopes.GetAsync(id, ct);
        if (envelope is null) return NotFound();
        var bytes = await _blobs.ReadAsync(envelope.WorkingBlobPath, ct);
        return File(bytes, "application/pdf", envelope.OriginalFileName);
    }

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        await _workflow.DeclineAsync(id, GetUserId(), ct);
        await _hub.Clients.Group($"envelope:{id}")
            .SendAsync("StatusChanged", new { status = "Declined" }, ct);
        return Ok();
    }

    private Guid GetUserId()
        => Guid.Parse(User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID claim missing."));
}