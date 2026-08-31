using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DUTCognitiveServicesApp.Models.ViewModels;

public class ImageUploadViewModel
{
    [Display(Name = "Select Image File")]
    public IFormFile? ImageFile { get; set; }

    [Display(Name = "Or Enter Image URL")]
    public string? ImageUrl { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    
    public bool HasSampleImages { get; set; } = true;
    public List<SampleImageItem> SampleImages { get; set; } = new();
}

public class SampleImageItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
