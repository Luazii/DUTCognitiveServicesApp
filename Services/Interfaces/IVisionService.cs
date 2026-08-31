using DUTCognitiveServicesApp.Models;

namespace DUTCognitiveServicesApp.Services.Interfaces;

public interface IVisionService
{
    Task<ImageAnalysisResult> AnalyzeImageAsync(Stream imageStream, string fileName, string blobUrl, string blobName);
    Task<ImageAnalysisResult> AnalyzeImageUrlAsync(string imageUrl, string fileName);
    bool IsConfigured { get; }
}
