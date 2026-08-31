namespace DUTCognitiveServicesApp.Models;

public class ImageAnalysisResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    
    // AI Cognitive Services Output (Rubric 4.1, 4.2)
    public string Caption { get; set; } = string.Empty;
    public double CaptionConfidence { get; set; }
    public List<VisionTag> Tags { get; set; } = new();
    public List<string> DenseCaptions { get; set; } = new();
    public List<string> ObjectsDetected { get; set; } = new();
    
    // Image Properties
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "JPEG";
    public long FileSizeBytes { get; set; }
    public string FormattedFileSize => FormatBytes(FileSizeBytes);

    // Caching Information (Rubric 5)
    public bool IsFromCache { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public DateTime? CachedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    
    // Telemetry & Execution
    public long ExecutionTimeMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string SourceProvider { get; set; } = "Azure Cognitive Services (Computer Vision)";

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}

public class VisionTag
{
    public string Name { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string ConfidencePercent => $"{Confidence * 100:F1}%";
}
