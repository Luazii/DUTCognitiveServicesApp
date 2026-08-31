using DUTCognitiveServicesApp.Models.ViewModels;

namespace DUTCognitiveServicesApp.Services.Interfaces;

public interface IBlobStorageService
{
    Task<(string BlobUrl, string BlobName)> UploadAsync(Stream stream, string fileName, string contentType);
    Task<Stream?> DownloadAsync(string blobName);
    Task<List<BlobItemSummary>> ListBlobsAsync();
    Task<bool> DeleteBlobAsync(string blobName);
    bool IsConfigured { get; }
}
