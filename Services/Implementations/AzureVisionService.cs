using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DUTCognitiveServicesApp.Models;
using DUTCognitiveServicesApp.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DUTCognitiveServicesApp.Services.Implementations;

public class AzureVisionService : IVisionService
{
    private readonly HttpClient _httpClient;
    private readonly AzureVisionSettings _settings;
    private readonly AppFeatureSettings _appSettings;
    private readonly ILogger<AzureVisionService> _logger;
    private readonly MockVisionAndStorageService _mockService;

    public bool IsConfigured => _settings.IsConfigured;

    public AzureVisionService(
        HttpClient httpClient,
        IOptions<AzureVisionSettings> settings,
        IOptions<AppFeatureSettings> appSettings,
        ILogger<AzureVisionService> logger,
        MockVisionAndStorageService mockService)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _appSettings = appSettings.Value;
        _logger = logger;
        _mockService = mockService;
    }

    public async Task<ImageAnalysisResult> AnalyzeImageAsync(Stream imageStream, string fileName, string blobUrl, string blobName)
    {
        if (!_settings.IsConfigured)
        {
            if (!_appSettings.UseMockIfUnconfigured)
            {
                throw new InvalidOperationException("Azure Cognitive Services credentials are not configured in appsettings.json and Mock Mode is disabled.");
            }
            _logger.LogInformation("Azure Cognitive Services is not configured with valid API keys. Using simulated AI response.");
            return await _mockService.AnalyzeImageAsync(imageStream, fileName, blobUrl, blobName);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var endpoint = _settings.Endpoint.TrimEnd('/');

            // Try Vision 3.2 Endpoint first (fully supported in all regions including South Africa North)
            imageStream.Position = 0;
            var legacyResult = await TryAnalyzeLegacyAsync(imageStream, fileName, blobUrl, blobName, stopwatch);
            if (legacyResult != null)
            {
                return legacyResult;
            }

            // Otherwise try v4.0 API
            imageStream.Position = 0;
            using var content = new StreamContent(imageStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var requestUrl = $"{endpoint}/computervision/imageanalysis:analyze?api-version=2023-10-01&features=caption,denseCaptions,tags,objects";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Azure Cognitive Services failed with HTTP {Status}: {Error}", response.StatusCode, errorBody);
                
                if (!_appSettings.UseMockIfUnconfigured)
                {
                    throw new InvalidOperationException($"Azure AI Vision API request failed with HTTP {response.StatusCode} ({response.ReasonPhrase}): {errorBody}");
                }

                var fallback = await _mockService.AnalyzeImageAsync(imageStream, fileName, blobUrl, blobName);
                fallback.SourceProvider = $"Simulation (Azure error: {response.StatusCode})";
                return fallback;
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var result = new ImageAnalysisResult
            {
                FileName = fileName,
                BlobName = blobName,
                BlobUrl = blobUrl,
                FileSizeBytes = imageStream.Length,
                SourceProvider = "Microsoft Azure AI Vision (Image Analysis 4.0)"
            };

            if (root.TryGetProperty("captionResult", out var captionResult))
            {
                result.Caption = captionResult.GetProperty("text").GetString() ?? "No description generated.";
                result.CaptionConfidence = captionResult.GetProperty("confidence").GetDouble();
            }

            if (root.TryGetProperty("denseCaptionsResult", out var denseCaptions) && 
                denseCaptions.TryGetProperty("values", out var values))
            {
                foreach (var item in values.EnumerateArray())
                {
                    var text = item.GetProperty("text").GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        result.DenseCaptions.Add(text);
                    }
                }
            }

            if (root.TryGetProperty("tagsResult", out var tagsResult) && 
                tagsResult.TryGetProperty("values", out var tagValues))
            {
                foreach (var tagItem in tagValues.EnumerateArray())
                {
                    result.Tags.Add(new VisionTag
                    {
                        Name = tagItem.GetProperty("name").GetString() ?? "",
                        Confidence = tagItem.GetProperty("confidence").GetDouble()
                    });
                }
            }

            if (root.TryGetProperty("objectsResult", out var objectsResult) && 
                objectsResult.TryGetProperty("values", out var objValues))
            {
                foreach (var objItem in objValues.EnumerateArray())
                {
                    var tagsArray = objItem.GetProperty("tags");
                    foreach (var tag in tagsArray.EnumerateArray())
                    {
                        result.ObjectsDetected.Add(tag.GetProperty("name").GetString() ?? "");
                    }
                }
            }

            if (root.TryGetProperty("metadata", out var meta))
            {
                result.Width = meta.GetProperty("width").GetInt32();
                result.Height = meta.GetProperty("height").GetInt32();
            }

            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Exception communicating with Azure Cognitive Services.");
            if (!_appSettings.UseMockIfUnconfigured)
            {
                throw new InvalidOperationException($"Error communicating with Azure Cognitive Services: {ex.Message}", ex);
            }
            return await _mockService.AnalyzeImageAsync(imageStream, fileName, blobUrl, blobName);
        }
    }

    public async Task<ImageAnalysisResult> AnalyzeImageUrlAsync(string imageUrl, string fileName)
    {
        if (!_settings.IsConfigured)
        {
            if (!_appSettings.UseMockIfUnconfigured)
            {
                throw new InvalidOperationException("Azure Cognitive Services credentials are not configured and Mock Mode is disabled.");
            }
            return await _mockService.AnalyzeImageUrlAsync(imageUrl, fileName);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var endpoint = _settings.Endpoint.TrimEnd('/');
            var legacyUrl = $"{endpoint}/vision/v3.2/analyze?visualFeatures=Categories,Description,Tags,Objects";

            using var request = new HttpRequestMessage(HttpMethod.Post, legacyUrl);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new { url = imageUrl }), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                if (!_appSettings.UseMockIfUnconfigured)
                {
                    throw new InvalidOperationException($"Azure AI Vision URL analysis failed with HTTP {response.StatusCode}: {errorBody}");
                }
                return await _mockService.AnalyzeImageUrlAsync(imageUrl, fileName);
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var result = new ImageAnalysisResult
            {
                FileName = fileName,
                BlobName = Path.GetFileName(new Uri(imageUrl).LocalPath),
                BlobUrl = imageUrl,
                SourceProvider = "Microsoft Azure Cognitive Services Vision"
            };

            if (root.TryGetProperty("description", out var desc) && desc.TryGetProperty("captions", out var caps))
            {
                var first = caps.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined)
                {
                    result.Caption = first.GetProperty("text").GetString() ?? "";
                    result.CaptionConfidence = first.GetProperty("confidence").GetDouble();
                }
            }

            if (root.TryGetProperty("tags", out var tagsArray))
            {
                foreach (var t in tagsArray.EnumerateArray())
                {
                    result.Tags.Add(new VisionTag
                    {
                        Name = t.GetProperty("name").GetString() ?? "",
                        Confidence = t.GetProperty("confidence").GetDouble()
                    });
                }
            }

            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to analyze image from URL.");
            if (!_appSettings.UseMockIfUnconfigured)
            {
                throw new InvalidOperationException($"Failed to analyze image from URL: {ex.Message}", ex);
            }
            return await _mockService.AnalyzeImageUrlAsync(imageUrl, fileName);
        }
    }

    private async Task<ImageAnalysisResult?> TryAnalyzeLegacyAsync(
        Stream imageStream, 
        string fileName, 
        string blobUrl, 
        string blobName, 
        Stopwatch stopwatch)
    {
        try
        {
            var endpoint = _settings.Endpoint.TrimEnd('/');
            var legacyUrl = $"{endpoint}/vision/v3.2/analyze?visualFeatures=Categories,Description,Tags,Objects";

            imageStream.Position = 0;
            using var content = new StreamContent(imageStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var request = new HttpRequestMessage(HttpMethod.Post, legacyUrl);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("v3.2 analyze returned HTTP {Status}: {Err}", response.StatusCode, err);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new ImageAnalysisResult
            {
                FileName = fileName,
                BlobName = blobName,
                BlobUrl = blobUrl,
                FileSizeBytes = imageStream.Length,
                SourceProvider = "Microsoft Azure AI Cognitive Services"
            };

            if (root.TryGetProperty("description", out var desc) && desc.TryGetProperty("captions", out var caps))
            {
                var first = caps.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined)
                {
                    result.Caption = first.GetProperty("text").GetString() ?? "";
                    result.CaptionConfidence = first.GetProperty("confidence").GetDouble();
                }
            }

            if (root.TryGetProperty("tags", out var tagsArray))
            {
                foreach (var t in tagsArray.EnumerateArray())
                {
                    result.Tags.Add(new VisionTag
                    {
                        Name = t.GetProperty("name").GetString() ?? "",
                        Confidence = t.GetProperty("confidence").GetDouble()
                    });
                }
            }

            if (root.TryGetProperty("objects", out var objectsArray))
            {
                foreach (var obj in objectsArray.EnumerateArray())
                {
                    var objName = obj.GetProperty("object").GetString();
                    if (!string.IsNullOrEmpty(objName))
                    {
                        result.ObjectsDetected.Add(objName);
                    }
                }
            }

            if (root.TryGetProperty("metadata", out var meta))
            {
                result.Width = meta.GetProperty("width").GetInt32();
                result.Height = meta.GetProperty("height").GetInt32();
                result.Format = meta.GetProperty("format").GetString() ?? "JPEG";
            }

            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryAnalyzeLegacyAsync exception");
            return null;
        }
    }
}
