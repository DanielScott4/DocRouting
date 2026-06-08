using MediatR;
using PadesSign.Application.Interfaces;
using PadesSign.Domain.Entities;

namespace PadesSign.Application.Commands;

public record UploadDocumentCommand(
    Guid   TemplateId,
    Guid   UploadedByUserId,
    string FileName,
    byte[] PdfBytes) : IRequest<Guid>;

public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, Guid>
{
    private readonly IBlobStorage         _blobs;
    private readonly IEnvelopeRepository  _envelopes;
    private readonly ITemplateRepository  _templates;

    public UploadDocumentHandler(IBlobStorage blobs, IEnvelopeRepository envelopes,
        ITemplateRepository templates)
    { _blobs = blobs; _envelopes = envelopes; _templates = templates; }

    public async Task<Guid> Handle(UploadDocumentCommand cmd, CancellationToken ct)
    {
        if (await _templates.GetAsync(cmd.TemplateId, ct) is null)
            throw new ArgumentException($"Template {cmd.TemplateId} not found.");

        var path     = $"originals/{Guid.NewGuid()}/{cmd.FileName}";
        await _blobs.WriteAsync(path, cmd.PdfBytes, ct: ct);

        var envelope = DocumentEnvelope.Create(cmd.TemplateId, cmd.UploadedByUserId,
            cmd.FileName, path);
        await _envelopes.AddAsync(envelope, ct);
        return envelope.Id;
    }
}