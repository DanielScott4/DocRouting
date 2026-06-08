using System.Net.Http.Json;

namespace PadesSign.Web.Services;

public class SigningApiClient
{
    private readonly HttpClient _http;
    public SigningApiClient(HttpClient http) => _http = http;

    public Task<UploadResponse?> UploadAsync(Guid templateId, byte[] pdfBytes, string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(pdfBytes), "file", fileName);
        form.Add(new StringContent(templateId.ToString()), "templateId");
        return _http.PostAsync("api/documents/upload", form)
            .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<UploadResponse>()).Unwrap();
    }

    public Task<PrepareResponse?> PrepareAsync(Guid envelopeId, string certBase64)
        => _http.PostAsJsonAsync($"api/signing/{envelopeId}/prepare",
               new { CertificateBase64 = certBase64 })
           .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<PrepareResponse>()).Unwrap();

    public Task FinalizeAsync(Guid sessionId, string pkcs7Base64, string[] chainBase64)
        => _http.PostAsJsonAsync($"api/signing/sessions/{sessionId}/finalize",
               new { Pkcs7SignatureBase64 = pkcs7Base64, ChainBase64 = chainBase64 });

    public Task<List<EnvelopeDto>?> ListEnvelopesAsync()
        => _http.GetFromJsonAsync<List<EnvelopeDto>>("api/documents");

    public Task<EnvelopeDto?> GetEnvelopeAsync(Guid id)
        => _http.GetFromJsonAsync<EnvelopeDto>($"api/documents/{id}");
}

public record UploadResponse(Guid EnvelopeId);
public record PrepareResponse(Guid Id, string DigestBase64, string DigestAlgorithm);
public record EnvelopeDto(Guid Id, string OriginalFileName, string Status, DateTime CreatedAt);