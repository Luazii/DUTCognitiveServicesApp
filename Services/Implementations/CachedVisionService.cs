using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using DUTCognitiveServicesApp.Models;
using DUTCognitiveServicesApp.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DUTCognitiveServicesApp.Services.Implementations;

public class CachedVisionService : IImageCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<CachedVisionService> _logger;
    private static readonly ConcurrentDictionary<string, (DateTime CachedAt, DateTime ExpiresAt)> _cacheRegistry = new();

    public CachedVisionService(
        IMemoryCache memoryCache,
        IOptions<CacheSettings> cacheSettings,
        ILogger<CachedVisionService> logger)
    {
        _memoryCache = memoryCache;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    public async Task<ImageAnalysisResult> GetOrAnalyzeAsync(
        string cacheKey, 
        Func<Task<ImageAnalysisResult>> analyzeFactory, 
        TimeSpan? expiration = null)
    {
        var timer = Stopwatch.StartNew();

        if (_memoryCache.TryGetValue(cacheKey, out ImageAnalysisResult? cachedResult) && cachedResult != null)
        {
            timer.Stop();
            _logger.LogInformation("🎯 CACHE HIT: Image analysis retrieved from memory cache for key: {Key}", cacheKey);

            // Clone to avoid mutating original cached instance
            var result = CloneResult(cachedResult);
            result.IsFromCache = true;
            result.ExecutionTimeMs = timer.ElapsedMilliseconds;
            
            if (_cacheRegistry.TryGetValue(cacheKey, out var meta))
            {
                result.CachedAt = meta.CachedAt;
                result.ExpiresAt = meta.ExpiresAt;
            }

            return result;
        }

        _logger.LogInformation("⚡ CACHE MISS: Invoking AI Cognitive Services for key: {Key}", cacheKey);

        var freshResult = await analyzeFactory();
        freshResult.CacheKey = cacheKey;
        freshResult.IsFromCache = false;

        var duration = expiration ?? TimeSpan.FromMinutes(_cacheSettings.DurationMinutes);
        var cachedAt = DateTime.UtcNow;
        var expiresAt = cachedAt.Add(duration);

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(duration)
            .SetPriority(CacheItemPriority.Normal)
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                _cacheRegistry.TryRemove(key.ToString() ?? "", out _);
                _logger.LogInformation("Cache entry evicted: {Key}, Reason: {Reason}", key, reason);
            });

        _memoryCache.Set(cacheKey, freshResult, cacheEntryOptions);
        _cacheRegistry[cacheKey] = (cachedAt, expiresAt);

        freshResult.CachedAt = cachedAt;
        freshResult.ExpiresAt = expiresAt;

        return freshResult;
    }

    public bool TryGet(string cacheKey, out ImageAnalysisResult? result)
    {
        return _memoryCache.TryGetValue(cacheKey, out result);
    }

    public void Set(string cacheKey, ImageAnalysisResult result, TimeSpan? expiration = null)
    {
        var duration = expiration ?? TimeSpan.FromMinutes(_cacheSettings.DurationMinutes);
        var cachedAt = DateTime.UtcNow;
        var expiresAt = cachedAt.Add(duration);

        _memoryCache.Set(cacheKey, result, duration);
        _cacheRegistry[cacheKey] = (cachedAt, expiresAt);
    }

    public void Remove(string cacheKey)
    {
        _memoryCache.Remove(cacheKey);
        _cacheRegistry.TryRemove(cacheKey, out _);
    }

    public void Clear()
    {
        foreach (var key in _cacheRegistry.Keys)
        {
            _memoryCache.Remove(key);
        }
        _cacheRegistry.Clear();
    }

    public List<string> GetAllKeys()
    {
        return _cacheRegistry.Keys.ToList();
    }

    public static string ComputeStreamHash(Stream stream)
    {
        stream.Position = 0;
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        stream.Position = 0;
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static ImageAnalysisResult CloneResult(ImageAnalysisResult orig)
    {
        return new ImageAnalysisResult
        {
            Id = orig.Id,
            FileName = orig.FileName,
            BlobName = orig.BlobName,
            BlobUrl = orig.BlobUrl,
            Caption = orig.Caption,
            CaptionConfidence = orig.CaptionConfidence,
            Width = orig.Width,
            Height = orig.Height,
            Format = orig.Format,
            FileSizeBytes = orig.FileSizeBytes,
            SourceProvider = orig.SourceProvider,
            DenseCaptions = new List<string>(orig.DenseCaptions),
            ObjectsDetected = new List<string>(orig.ObjectsDetected),
            Tags = orig.Tags.Select(t => new VisionTag { Name = t.Name, Confidence = t.Confidence }).ToList(),
            CacheKey = orig.CacheKey,
            CreatedAt = orig.CreatedAt
        };
    }
}
