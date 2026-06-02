using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using PadesSign.Application.Interfaces;

namespace PadesSign.Infrastructure.Storage;

public class AzureBlobStorage : IBlobStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorage(string connectionString, string containerName)
    {
        var svc = new BlobServiceClient(connectionString);
        _container = svc.GetBlobContainerClient(containerName);
        _container.CreateIfNotExists();
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(path);
        var dl   = await blob.DownloadContentAsync(ct);
        return dl.Value.Content.ToArray();
    }

    public async Task<string> WriteAsync(string path, byte[] data, string contentType = "application/pdf", CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(path);
        using var ms = new MemoryStream(data);
        await blob.UploadAsync(ms, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
        return path;
    }

    public async Task DeleteAsync(string path, CancellationToken ct)
        => await _container.GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: ct);

    public Task<string> GetDownloadUrlAsync(string path, TimeSpan ttl, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(path);
        var sas  = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(ttl));
        return Task.FromResult(sas.ToString());
    }
}