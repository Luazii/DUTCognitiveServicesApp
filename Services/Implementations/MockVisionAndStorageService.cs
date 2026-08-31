using System.Diagnostics;
using System.Security.Cryptography;
using DUTCognitiveServicesApp.Models;

namespace DUTCognitiveServicesApp.Services.Implementations;

public class MockVisionAndStorageService
{
    private static readonly (string Keyword, string Caption, double Confidence, string[] Tags, string[] Objects)[] Presets = new[]
    {
        ("city", "A panoramic view of a modern cityscape with skyscrapers under a clear blue sky", 0.94, new[] { "skyscraper", "sky", "building", "metropolis", "architecture", "urban", "downtown" }, new[] { "building", "sky" }),
        ("dog", "A playful golden retriever sitting on lush green grass in a sunny park", 0.98, new[] { "dog", "animal", "pet", "canine", "grass", "outdoor", "golden retriever", "happy" }, new[] { "dog", "plant" }),
        ("cat", "A domestic short-haired cat relaxing comfortably on a sofa", 0.96, new[] { "cat", "feline", "pet", "couch", "indoor", "whiskers", "cute" }, new[] { "cat", "couch" }),
        ("nature", "A breathtaking mountain landscape with dense pine forests and a calm alpine lake", 0.95, new[] { "mountain", "lake", "forest", "wilderness", "scenery", "reflection", "nature" }, new[] { "mountain", "tree", "water" }),
        ("car", "A sleek luxury sports car parked on an asphalt street during sunset", 0.92, new[] { "car", "vehicle", "sports car", "automobile", "asphalt", "transportation", "wheel" }, new[] { "car", "wheel" }),
        ("food", "A delicious gourmet plate with fresh vegetables, grilled steak, and artisan garnish", 0.93, new[] { "food", "dish", "meal", "cuisine", "gourmet", "delicious", "restaurant" }, new[] { "food", "plate" }),
        ("person", "A smiling professional person standing in a contemporary office setting", 0.91, new[] { "person", "human", "face", "smile", "professional", "indoor", "portrait" }, new[] { "person" }),
        ("student", "A DUT university student working on a laptop in a bright modern campus library", 0.97, new[] { "student", "laptop", "university", "technology", "education", "study", "campus" }, new[] { "person", "laptop" })
    };

    public async Task<ImageAnalysisResult> AnalyzeImageAsync(Stream imageStream, string fileName, string blobUrl, string blobName)
    {
        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(350); // Simulate API latency

        var nameLower = fileName.ToLowerInvariant();
        var preset = Presets.FirstOrDefault(p => nameLower.Contains(p.Keyword));

        if (preset.Caption == null)
        {
            // Pick a deterministic preset based on file name hash
            var hash = Math.Abs(fileName.GetHashCode()) % Presets.Length;
            preset = Presets[hash];
        }

        stopwatch.Stop();

        var result = new ImageAnalysisResult
        {
            FileName = fileName,
            BlobName = blobName,
            BlobUrl = blobUrl,
            Caption = preset.Caption,
            CaptionConfidence = preset.Confidence,
            Width = 1920,
            Height = 1080,
            Format = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant(),
            FileSizeBytes = imageStream.Length > 0 ? imageStream.Length : 256000,
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            SourceProvider = "Simulated Cognitive Services Engine (Offline Demo Mode)",
            DenseCaptions = new List<string>
            {
                preset.Caption,
                $"Foreground element with high clarity ({preset.Tags.FirstOrDefault() ?? "object"})",
                "Ambient lighting with natural contrast balance"
            },
            ObjectsDetected = preset.Objects.ToList(),
            Tags = preset.Tags.Select((tag, idx) => new VisionTag
            {
                Name = tag,
                Confidence = Math.Clamp(preset.Confidence - (idx * 0.04), 0.70, 0.99)
            }).ToList()
        };

        return result;
    }

    public async Task<ImageAnalysisResult> AnalyzeImageUrlAsync(string imageUrl, string fileName)
    {
        using var dummyStream = new MemoryStream(new byte[1024]);
        return await AnalyzeImageAsync(dummyStream, fileName, imageUrl, Path.GetFileName(imageUrl));
    }
}
