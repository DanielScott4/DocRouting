namespace PadesSign.Domain.Entities;

/// <summary>
/// Short-lived server-side session created during the prepare step.
/// Destroyed after finalize or TTL expiry.
/// </summary>
public class SigningSession
{
    public Guid     Id               { get; init; } = Guid.NewGuid();
    public Guid     EnvelopeId       { get; init; }
    public Guid     UserId           { get; init; }
    public int      StepOrder        { get; init; }
    public string   PreparedBlobPath { get; init; } = string.Empty;
    public string   DigestBase64     { get; init; } = string.Empty;
    public string   DigestAlgorithm  { get; init; } = "SHA-256";
    public long[]   ByteRange        { get; init; } = Array.Empty<long>();
    public DateTime ExpiresAt        { get; init; } = DateTime.UtcNow.AddMinutes(10);
    public bool     IsExpired        => DateTime.UtcNow > ExpiresAt;
}