namespace DUTCognitiveServicesApp.Models.ViewModels;

public class ImageDetailsViewModel
{
    public ImageAnalysisResult Result { get; set; } = new();
    public string? StatusMessage { get; set; }
    public bool IsNewUpload { get; set; }
}
