using DUTCognitiveServicesApp.Models;

namespace DUTCognitiveServicesApp.Services.Interfaces;

public interface IImageCacheService
{
    Task<ImageAnalysisResult> GetOrAnalyzeAsync(
        string cacheKey, 
        Func<Task<ImageAnalysisResult>> analyzeFactory, 
        TimeSpan? expiration = null);

    bool TryGet(string cacheKey, out ImageAnalysisResult? result);
    void Set(string cacheKey, ImageAnalysisResult result, TimeSpan? expiration = null);
    void Remove(string cacheKey);
    void Clear();
    List<string> GetAllKeys();
}
