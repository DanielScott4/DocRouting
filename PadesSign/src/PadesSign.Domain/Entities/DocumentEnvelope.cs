using PadesSign.Domain.Enums;

namespace PadesSign.Domain.Entities;

public class DocumentEnvelope
{
    public Guid           Id                { get; private set; } = Guid.NewGuid();
    public Guid           TemplateId        { get; private set; }
    public Guid           UploadedByUserId  { get; private set; }
    public string         OriginalFileName  { get; private set; } = string.Empty;
    /// <summary>Blob path of the original (unsigned) PDF.</summary>
    public string         OriginalBlobPath  { get; private set; } = string.Empty;
    /// <summary>Blob path of the current working copy (grows with each applied sig).</summary>
    public string         WorkingBlobPath   { get; private set; } = string.Empty;
    public EnvelopeStatus Status            { get; private set; } = EnvelopeStatus.Draft;
    public int            CurrentStepOrder  { get; private set; }
    public DateTime       CreatedAt         { get; private set; } = DateTime.UtcNow;
    public DateTime       UpdatedAt         { get; private set; } = DateTime.UtcNow;

    private readonly List<SignatureRecord> _signatures = new();
    public IReadOnlyList<SignatureRecord>  Signatures  => _signatures.AsReadOnly();

    private DocumentEnvelope() { }

    public static DocumentEnvelope Create(Guid templateId, Guid uploadedByUserId,
        string originalFileName, string blobPath)
        => new()
        {
            TemplateId       = templateId,
            UploadedByUserId = uploadedByUserId,
            OriginalFileName = originalFileName,
            OriginalBlobPath = blobPath,
            WorkingBlobPath  = blobPath,
            Status           = EnvelopeStatus.Draft
        };

    public void Start(int firstStepOrder)
    {
        Status           = EnvelopeStatus.InProgress;
        CurrentStepOrder = firstStepOrder;
        UpdatedAt        = DateTime.UtcNow;
    }

    public void RecordSignature(SignatureRecord sig, string newWorkingBlobPath)
    {
        _signatures.Add(sig);
        WorkingBlobPath = newWorkingBlobPath;
        UpdatedAt       = DateTime.UtcNow;
    }

    public void AdvanceToStep(int stepOrder)
    {
        CurrentStepOrder = stepOrder;
        UpdatedAt        = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status    = EnvelopeStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Decline()
    {
        Status    = EnvelopeStatus.Declined;
        UpdatedAt = DateTime.UtcNow;
    }
}