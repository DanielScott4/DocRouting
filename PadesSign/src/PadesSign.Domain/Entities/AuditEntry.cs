namespace PadesSign.Domain.Entities;

public class AuditEntry
{
    public Guid     Id         { get; init; } = Guid.NewGuid();
    public Guid?    EnvelopeId { get; init; }
    public Guid?    UserId     { get; init; }
    public string   Action     { get; init; } = string.Empty;
    public string   Detail     { get; init; } = string.Empty;
    public string   IpAddress  { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}