namespace DUTCognitiveServicesApp.Models.ViewModels;

public class GalleryViewModel
{
    public List<BlobItemSummary> Blobs { get; set; } = new();
    public int TotalCount => Blobs.Count;
    public string? StatusMessage { get; set; }
}

public class BlobItemSummary
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset? CreatedOn { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
    public bool HasCachedAnalysis { get; set; }
    public string? CachedCaption { get; set; }
}
