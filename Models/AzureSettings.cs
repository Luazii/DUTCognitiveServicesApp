namespace DUTCognitiveServicesApp.Models;

public class AzureBlobSettings
{
    public const string SectionName = "AzureBlobStorage";
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "image-uploads";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString) && 
                                !ConnectionString.Contains("YOUR_AZURE_STORAGE_CONNECTION_STRING");
}

public class AzureVisionSettings
{
    public const string SectionName = "AzureCognitiveServices";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint) && 
                                !string.IsNullOrWhiteSpace(ApiKey) && 
                                !ApiKey.Contains("YOUR_COMPUTER_VISION_KEY");
}

public class CacheSettings
{
    public const string SectionName = "CacheSettings";
    public int DurationMinutes { get; set; } = 30;
}

public class AppFeatureSettings
{
    public const string SectionName = "AppSettings";
    public bool UseMockIfUnconfigured { get; set; } = true;
}
