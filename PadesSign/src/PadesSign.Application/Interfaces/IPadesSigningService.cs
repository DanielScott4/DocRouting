using Org.BouncyCastle.X509;
using PadesSign.Domain.Entities;
using PadesSign.Domain.ValueObjects;

namespace PadesSign.Application.Interfaces;

public interface IPadesSigningService
{
    /// <summary>
    /// Reserves the signature placeholder in the PDF and returns the byte-range digest
    /// the smartcard must sign.
    /// </summary>
    Task<SigningSession> PrepareAsync(
        Guid                    envelopeId,
        Guid                    userId,
        int                     stepOrder,
        string                  sourceBlobPath,
        SignatureFieldDefinition field,
        X509Certificate         signerCert,
        CancellationToken       ct = default);

    /// <summary>
    /// Embeds the CMS signature blob, adds the TSA timestamp, and writes the
    /// PAdES-B-LT PDF to blob storage.
    /// </summary>
    Task<string> FinalizeAsync(
        SigningSession     session,
        byte[]            pkcs7SignatureBytes,
        X509Certificate[] chain,
        CancellationToken ct = default);
}