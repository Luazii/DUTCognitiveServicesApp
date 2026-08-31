using DUTCognitiveServicesApp.Models;
using DUTCognitiveServicesApp.Services.Implementations;
using DUTCognitiveServicesApp.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

// 2. Bind Strongly Typed Configuration Options (OOP Options Pattern)
builder.Services.Configure<AzureBlobSettings>(
    builder.Configuration.GetSection(AzureBlobSettings.SectionName));
builder.Services.Configure<AzureVisionSettings>(
    builder.Configuration.GetSection(AzureVisionSettings.SectionName));
builder.Services.Configure<CacheSettings>(
    builder.Configuration.GetSection(CacheSettings.SectionName));
builder.Services.Configure<AppFeatureSettings>(
    builder.Configuration.GetSection(AppFeatureSettings.SectionName));

// 3. Register Core Services & Memory Cache (Rubric 5 & 8)
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<AzureVisionService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// 4. Dependency Injection Registrations
builder.Services.AddSingleton<MockVisionAndStorageService>();
builder.Services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
builder.Services.AddScoped<IVisionService, AzureVisionService>();
builder.Services.AddSingleton<IImageCacheService, CachedVisionService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
