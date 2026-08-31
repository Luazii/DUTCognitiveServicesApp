using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DUTCognitiveServicesApp.Models;
using DUTCognitiveServicesApp.Models.ViewModels;
using DUTCognitiveServicesApp.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DUTCognitiveServicesApp.Services.Implementations;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly AzureBlobSettings _settings;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private readonly BlobContainerClient? _containerClient;

    public bool IsConfigured => _settings.IsConfigured && _containerClient != null;

    public AzureBlobStorageService(
        IOptions<AzureBlobSettings> settings,
        IWebHostEnvironment env,
        ILogger<AzureBlobStorageService> logger)
    {
        _settings = settings.Value;
        _env = env;
        _logger = logger;

        if (_settings.IsConfigured)
        {
            try
            {
                var serviceClient = new BlobServiceClient(_settings.ConnectionString);
                _containerClient = serviceClient.GetBlobContainerClient(_settings.ContainerName);
                
                try
                {
                    _containerClient.CreateIfNotExists(PublicAccessType.Blob);
                }
                catch (Azure.RequestFailedException ex) when (ex.ErrorCode == "PublicAccessNotPermitted" || ex.Status == 409)
                {
                    _logger.LogInformation("Storage account has public access disabled. Creating/accessing container with default access.");
                    try
                    {
                        _containerClient.CreateIfNotExists(PublicAccessType.None);
                    }
                    catch (Exception createEx)
                    {
                        _logger.LogInformation("Container already exists: {Msg}", createEx.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Container check notice: {Msg}", ex.Message);
                }

                _logger.LogInformation("Azure Blob Storage initialized for container: {Container}", _settings.ContainerName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to Azure Blob Storage with provided connection string. Falling back to local storage simulation.");
                _containerClient = null;
            }
        }
        else
        {
            _logger.LogInformation("Azure Blob Storage not configured. Using local filesystem emulation.");
        }
    }

    public async Task<(string BlobUrl, string BlobName)> UploadAsync(Stream stream, string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        var uniqueBlobName = $"{Guid.NewGuid():N}_{Path.GetFileNameWithoutExtension(fileName)}{extension}";

        if (_containerClient != null)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(uniqueBlobName);
                stream.Position = 0;
                
                var headers = new BlobHttpHeaders { ContentType = contentType };
                await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });

                string resolvedUrl = blobClient.Uri.ToString();
                if (blobClient.CanGenerateSasUri)
                {
                    var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddDays(7));
                    resolvedUrl = sasUri.ToString();
                }

                _logger.LogInformation("Uploaded blob to Azure Storage: {BlobName} -> {Url}", uniqueBlobName, resolvedUrl);
                return (resolvedUrl, uniqueBlobName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Blob upload failed. Falling back to local storage.");
            }
        }

        // Fallback: Local filesystem storage under wwwroot/uploads
        var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var filePath = Path.Combine(uploadsFolder, uniqueBlobName);
        stream.Position = 0;
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream);
        }

        var localUrl = $"/uploads/{uniqueBlobName}";
        _logger.LogInformation("Stored blob locally: {LocalUrl}", localUrl);
        return (localUrl, uniqueBlobName);
    }

    public async Task<Stream?> DownloadAsync(string blobName)
    {
        if (_containerClient != null)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);
                if (await blobClient.ExistsAsync())
                {
                    var response = await blobClient.DownloadStreamingAsync();
                    return response.Value.Content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download blob from Azure Storage: {BlobName}", blobName);
            }
        }

        var localPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", blobName);
        if (File.Exists(localPath))
        {
            return File.OpenRead(localPath);
        }

        return null;
    }

    public async Task<List<BlobItemSummary>> ListBlobsAsync()
    {
        var list = new List<BlobItemSummary>();

        if (_containerClient != null)
        {
            try
            {
                await foreach (var blobItem in _containerClient.GetBlobsAsync())
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    string url = blobClient.Uri.ToString();
                    if (blobClient.CanGenerateSasUri)
                    {
                        url = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddDays(7)).ToString();
                    }

                    list.Add(new BlobItemSummary
                    {
                        Name = blobItem.Name,
                        Url = url,
                        SizeBytes = blobItem.Properties.ContentLength ?? 0,
                        CreatedOn = blobItem.Properties.CreatedOn,
                        ContentType = blobItem.Properties.ContentType ?? "image/jpeg"
                    });
                }
                return list.OrderByDescending(b => b.CreatedOn).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list blobs from Azure Storage.");
            }
        }

        // Local fallback list
        var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
        if (Directory.Exists(uploadsFolder))
        {
            var files = Directory.GetFiles(uploadsFolder);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                list.Add(new BlobItemSummary
                {
                    Name = fileInfo.Name,
                    Url = $"/uploads/{fileInfo.Name}",
                    SizeBytes = fileInfo.Length,
                    CreatedOn = fileInfo.CreationTimeUtc,
                    ContentType = GetContentType(fileInfo.Extension)
                });
            }
        }

        return list.OrderByDescending(b => b.CreatedOn).ToList();
    }

    public async Task<bool> DeleteBlobAsync(string blobName)
    {
        if (_containerClient != null)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);
                return await blobClient.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete blob from Azure: {BlobName}", blobName);
            }
        }

        var localPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", blobName);
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
            return true;
        }

        return false;
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };
}
