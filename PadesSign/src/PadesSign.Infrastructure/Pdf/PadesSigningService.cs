using System.Security.Cryptography;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.X509;
using PadesSign.Application.Interfaces;
using PadesSign.Domain.Entities;
using PadesSign.Domain.ValueObjects;

namespace PadesSign.Infrastructure.Pdf;

/// <summary>
/// Two-phase PAdES-B-LT signing.
/// Phase 1 â€“ PrepareAsync: writes a PDF with a reserved signature hole and returns the digest.
/// Phase 2 â€“ FinalizeAsync: embeds the CMS blob, applies RFC 3161 timestamp, and writes PAdES-B-LT.
/// </summary>
public class PadesSigningService : IPadesSigningService
{
    private readonly IBlobStorage _blobs;
    private readonly string       _tsaUrl;
    private readonly string       _tsaLogin;
    private readonly string       _tsaPassword;

    // Reserve 32 KB for the CMS container (covers large RSA-4096 + OCSP inline)
    private const int SignatureReservedSize = 32_768;

    public PadesSigningService(IBlobStorage blobs, PadesOptions options)
    {
        _blobs       = blobs;
        _tsaUrl      = options.TsaUrl;
        _tsaLogin    = options.TsaLogin;
        _tsaPassword = options.TsaPassword;
    }

    // â”€â”€ Phase 1 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task<SigningSession> PrepareAsync(
        Guid envelopeId, Guid userId, int stepOrder,
        string sourceBlobPath, SignatureFieldDefinition field,
        X509Certificate signerCert, CancellationToken ct = default)
    {
        var pdfBytes = await _blobs.ReadAsync(sourceBlobPath, ct);
        using var output = new MemoryStream();

        var reader  = new PdfReader(new MemoryStream(pdfBytes));
        var signer  = new PdfSigner(reader, output,
            new StampingProperties().UseAppendMode());

        // Visible appearance
        var ap = signer.GetSignatureAppearance();
        ap.SetReason(field.Reason)
          .SetLocation(field.Location)
          .SetContact(field.ContactInfo)
          .SetPageRect(new iText.Kernel.Geom.Rectangle(
              field.X, field.Y, field.Width, field.Height))
          .SetPageNumber(field.PageNumber)
          .SetSignatureCreator("PadesSign 1.0");

        signer.SetFieldName($"sig_{envelopeId}_{stepOrder}");
        signer.SetSignDate(DateTime.UtcNow);
        signer.SetCertificationLevel(PdfSigner.NOT_CERTIFIED);

        // Reserve placeholder; MakeSignature writes the hole into output
        var container = new ExternalBlankSignatureContainer(
            PdfName.Adobe_PPKLite, PdfName.Adbe_pkcs7_detached);
        MakeSignature.SignExternalContainer(signer, container, SignatureReservedSize);

        var prepared  = output.ToArray();
        var byteRange = GetByteRange(prepared);
        var digest    = ComputeByteRangeHash(prepared, byteRange);

        var sessionId  = Guid.NewGuid();
        var blobPath   = $"sessions/{sessionId}/prepared.pdf";
        await _blobs.WriteAsync(blobPath, prepared, ct: ct);

        return new SigningSession
        {
            Id               = sessionId,
            EnvelopeId       = envelopeId,
            UserId           = userId,
            StepOrder        = stepOrder,
            PreparedBlobPath = blobPath,
            DigestBase64     = Convert.ToBase64String(digest),
            DigestAlgorithm  = "SHA-256",
            ByteRange        = byteRange
        };
    }

    // â”€â”€ Phase 2 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task<string> FinalizeAsync(
        SigningSession session, byte[] pkcs7SignatureBytes,
        X509Certificate[] chain, CancellationToken ct = default)
    {
        if (pkcs7SignatureBytes.Length > SignatureReservedSize)
            throw new InvalidOperationException(
                $"CMS blob ({pkcs7SignatureBytes.Length} bytes) exceeds reserved space.");

        var prepared = await _blobs.ReadAsync(session.PreparedBlobPath, ct);

        // Verify the digest still matches (integrity guard)
        var actualDigest = ComputeByteRangeHash(prepared, session.ByteRange);
        if (Convert.ToBase64String(actualDigest) != session.DigestBase64)
            throw new InvalidOperationException("Prepared PDF integrity check failed.");

        // Embed the CMS bytes into the hole
        var signed = (byte[])prepared.Clone();
        var hexSig = Convert.ToHexString(pkcs7SignatureBytes);
        var hexBytes = System.Text.Encoding.ASCII.GetBytes(hexSig
            .PadRight(SignatureReservedSize * 2, '0'));
        Array.Copy(hexBytes, 0, signed, session.ByteRange[0] + session.ByteRange[1] + 1,
            hexBytes.Length);

        // Add LTV: DSS dictionary with OCSP/CRL snapshots + RFC 3161 archive timestamp
        using var ltvInput  = new MemoryStream(signed);
        using var ltvOutput = new MemoryStream();
        var ltvVerification = new LtvVerification(
            new PdfDocument(new PdfReader(ltvInput),
                new PdfWriter(ltvOutput),
                new StampingProperties().UseAppendMode()));

        var fieldName = $"sig_{session.EnvelopeId}_{session.StepOrder}";
        ltvVerification.AddVerification(fieldName,
            new OcspClientBouncyCastle(null),
            new CrlClientOnline(),
            LtvVerification.CertificateOption.WHOLE_CHAIN,
            LtvVerification.Level.OCSP_OPTIONAL_CRL,
            LtvVerification.CertificateInclusion.YES);
        ltvVerification.Merge();

        // RFC 3161 timestamp
        var tsa        = new TsaClientBouncyCastle(_tsaUrl, _tsaLogin, _tsaPassword);
        var withTsa    = ApplyTimestamp(ltvOutput.ToArray(), fieldName, tsa);

        var signedPath = $"signed/{session.EnvelopeId}/{Guid.NewGuid()}.pdf";
        await _blobs.WriteAsync(signedPath, withTsa, ct: ct);
        return signedPath;
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static long[] GetByteRange(byte[] pdf)
    {
        // iText writes /ByteRange [0 A B C] near the signature field
        var marker = System.Text.Encoding.ASCII.GetBytes("/ByteRange [");
        var pos    = IndexOf(pdf, marker);
        if (pos < 0) throw new InvalidOperationException("ByteRange not found in prepared PDF.");
        pos += marker.Length;
        var end = Array.IndexOf(pdf, (byte)']', pos);
        var raw = System.Text.Encoding.ASCII.GetString(pdf, pos, end - pos)
            .Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return raw.Select(long.Parse).ToArray();
    }

    private static byte[] ComputeByteRangeHash(byte[] pdf, long[] br)
    {
        using var sha = SHA256.Create();
        using var ms  = new MemoryStream(pdf);
        var buf1 = new byte[br[1]];
        ms.Seek(br[0], SeekOrigin.Begin); ms.Read(buf1);
        sha.TransformBlock(buf1, 0, buf1.Length, null, 0);
        var buf2 = new byte[br[3]];
        ms.Seek(br[2], SeekOrigin.Begin); ms.Read(buf2);
        sha.TransformFinalBlock(buf2, 0, buf2.Length);
        return sha.Hash!;
    }

    private static byte[] ApplyTimestamp(byte[] pdf, string fieldName, ITSAClient tsa)
    {
        using var ms  = new MemoryStream(pdf);
        using var out2 = new MemoryStream();
        var doc    = new PdfDocument(new PdfReader(ms), new PdfWriter(out2),
                         new StampingProperties().UseAppendMode());
        var signer = new PdfSigner(new PdfReader(new MemoryStream(pdf)), out2,
                         new StampingProperties().UseAppendMode());
        // iText's PAdES-B-LT timestamps go via PdfSigner.Timestamp
        signer.Timestamp(tsa, fieldName + "_ts");
        return out2.ToArray();
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var found = true;
            for (var j = 0; j < needle.Length && found; j++)
                found = haystack[i + j] == needle[j];
            if (found) return i;
        }
        return -1;
    }
}

public class PadesOptions
{
    public string TsaUrl      { get; init; } = string.Empty;
    public string TsaLogin    { get; init; } = string.Empty;
    public string TsaPassword { get; init; } = string.Empty;
}