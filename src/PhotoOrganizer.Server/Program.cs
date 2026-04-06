using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PhotoOrganizer.Application;
using PhotoOrganizer.Application.Crawler;
using PhotoOrganizer.Application.Folders;
using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Infrastructure.Crawler;
using PhotoOrganizer.Infrastructure.Services;
using PhotoOrganizer.Infrastructure.Sidecars;
using PhotoOrganizer.Infrastructure.Storage;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/photo-organizer.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:6173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.Configure<CrawlerSettings>(builder.Configuration.GetSection("Crawler"));
builder.Services.AddSingleton<ICrawlerService, CrawlerService>();

builder.Services.Configure<PhotoOrganizerSettings>(builder.Configuration.GetSection("PhotoOrganizer"));
builder.Services.AddSingleton<ISidecarReader, SidecarReader>();
builder.Services.AddSingleton<IFolderRepository, FileSystemFolderRepository>();
builder.Services.AddSingleton<IPhotoRepository, FileSystemPhotoRepository>();
builder.Services.AddSingleton<IFolderService, FolderService>();
builder.Services.AddSingleton<IPhotoService, PhotoService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/folders", async (IFolderService service) =>
{
    var folders = await service.GetAllFoldersAsync();
    return Results.Ok(folders);
});

app.MapGet("/api/photos", async (
    [FromQuery] string? folder,
    [FromQuery] string? type,
    [FromQuery] bool deduplicated = true,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50,
    IPhotoService service = default!) =>
{
    var filter = new PhotoFilter
    {
        Folder = folder,
        Type = type,
        Deduplicated = deduplicated,
        Page = page,
        PageSize = pageSize
    };
    var result = await service.GetPhotosAsync(filter);
    return Results.Ok(result);
});

app.MapGet("/api/photos/{id:guid}", async (Guid id, IPhotoService service) =>
{
    var photo = await service.GetPhotoByIdAsync(id);
    return photo is null ? Results.NotFound() : Results.Ok(photo);
});

app.MapGet("/api/photos/{id:guid}/image", async (Guid id, IPhotoRepository repository) =>
{
    var photo = await repository.GetByIdAsync(id);
    if (photo is null)
        return Results.NotFound();

    var contentType = Path.GetExtension(photo.FilePath).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".heic" => "image/heic",
        ".tiff" or ".tif" => "image/tiff",
        _ => "application/octet-stream"
    };

    return Results.File(photo.FilePath, contentType);
});

app.MapGet("/api/slideshow/next", async (IPhotoService service) =>
{
    var page = await service.GetPhotosAsync(new PhotoFilter { Deduplicated = true, Page = 1, PageSize = int.MaxValue });
    if (page.TotalCount == 0)
        return Results.NotFound();

    var index = Random.Shared.Next(page.TotalCount);
    return Results.Ok(page.Items[index]);
});

app.MapGet("/api/config", (IOptions<PhotoOrganizerSettings> options) =>
    Results.Ok(options.Value));

app.MapPost("/api/crawler/start", async ([FromBody] StartCrawlRequest request, ICrawlerService service) =>
{
    var started = await service.StartCrawlAsync(request);
    return started ? Results.Accepted() : Results.Conflict("Crawler is already running");
});

app.MapGet("/api/crawler/status", async (ICrawlerService service) =>
{
    var status = await service.GetStatusAsync();
    return Results.Ok(status);
});

app.MapFallbackToFile("index.html");

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
