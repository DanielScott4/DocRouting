using PadesSign.Domain.ValueObjects;

namespace PadesSign.Domain.Entities;

public class SignatureRecord
{
    public Guid                     Id                   { get; init; } = Guid.NewGuid();
    public Guid                     EnvelopeId           { get; init; }
    public Guid                     SignedByUserId       { get; init; }
    public int                      StepOrder            { get; init; }
    public DateTime                 SignedAt             { get; init; } = DateTime.UtcNow;
    public string                   CertificateSubject   { get; init; } = string.Empty;
    public string                   CertificateThumbprint{ get; init; } = string.Empty;
    public string                   CertificateSerial    { get; init; } = string.Empty;
    public string                   IpAddress            { get; init; } = string.Empty;
    public SignatureFieldDefinition  Field                { get; init; } = null!;
}