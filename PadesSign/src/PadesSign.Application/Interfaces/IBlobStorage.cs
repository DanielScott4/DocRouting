namespace PadesSign.Application.Interfaces;

public interface IBlobStorage
{
    Task<byte[]>  ReadAsync(string path, CancellationToken ct = default);
    Task<string>  WriteAsync(string path, byte[] data, string contentType = "application/pdf", CancellationToken ct = default);
    Task          DeleteAsync(string path, CancellationToken ct = default);
    Task<string>  GetDownloadUrlAsync(string path, TimeSpan ttl, CancellationToken ct = default);
}