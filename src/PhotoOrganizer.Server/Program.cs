using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using PhotoOrganizer.Application;
using PhotoOrganizer.Application.Crawler;
using PhotoOrganizer.Application.Folders;
using PhotoOrganizer.Application.Index;
using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Infrastructure.Crawler;
using PhotoOrganizer.Infrastructure.Imaging;
using PhotoOrganizer.Infrastructure.Indexing;
using PhotoOrganizer.Infrastructure.Services;
using PhotoOrganizer.Infrastructure.Sidecars;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/photo-organizer.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CSRF: mutating endpoints require this custom header. A non-CORS-safelisted header forces a
// CORS preflight; the origin allowlist below then blocks disallowed origins so their preflight
// fails before the POST is ever sent. Simple cross-site requests cannot set custom headers,
// preventing blind-CSRF attacks while the app is loopback-only (the bind is never 0.0.0.0).
const string CsrfHeader = "X-Requested-With";

// CORS: allow both the Vite dev origin (6173) and the integrated server origin (6192).
// Driven by config (Cors:AllowedOrigins) so deployed origins can be added without rebuilding.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:6173", "http://localhost:6192"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.Configure<CrawlerSettings>(builder.Configuration.GetSection("Crawler"));
builder.Services.AddSingleton<ICrawlerService, CrawlerService>();

builder.Services.Configure<PhotoOrganizerSettings>(builder.Configuration.GetSection("PhotoOrganizer"));
builder.Services.AddSingleton<ISidecarReader, SidecarReader>();
builder.Services.AddSingleton<IImageTranscoder, MagickImageTranscoder>();

// Progressive randomized indexer + in-memory index (replaces FileSystem*Repository).
builder.Services.AddSingleton<PhotoIndex>();
builder.Services.AddSingleton<RandomizedSidecarIndexer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RandomizedSidecarIndexer>());
builder.Services.AddSingleton<IFolderRepository, IndexFolderRepository>();
builder.Services.AddSingleton<IPhotoRepository, IndexPhotoRepository>();

builder.Services.AddSingleton<IFolderService, FolderService>();
builder.Services.AddSingleton<IPhotoService, PhotoService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

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
    [FromQuery] string? cursor = null,
    [FromQuery] int? limit = null,
    [FromQuery] string? fileName = null,
    [FromQuery] string? dateFrom = null,
    [FromQuery] string? dateTo = null,
    IPhotoService service = default!) =>
{
    var filter = new PhotoFilter
    {
        Folder = folder,
        Type = type,
        Deduplicated = deduplicated,
        Page = page,
        PageSize = pageSize,
        Cursor = cursor,
        Limit = limit,
        FileName = fileName,
        // Parse dates leniently — an unparseable value is treated as absent (no 400).
        DateFrom = DateOnly.TryParse(dateFrom, out var df) ? df : null,
        DateTo = DateOnly.TryParse(dateTo, out var dt) ? dt : null,
    };
    var result = await service.GetPhotosAsync(filter);
    return Results.Ok(result);
});

app.MapGet("/api/photos/{id:guid}", async (Guid id, IPhotoService service) =>
{
    var photo = await service.GetPhotoByIdAsync(id);
    return photo is null ? Results.NotFound() : Results.Ok(photo);
});

app.MapGet("/api/photos/{id:guid}/image", async (Guid id, HttpContext httpContext, IPhotoRepository repository, IImageTranscoder transcoder, CancellationToken ct) =>
{
    var photo = await repository.GetByIdAsync(id);
    if (photo is null)
        return Results.NotFound();

    // Set Content-Disposition: inline so the browser renders the image inline while still
    // knowing the file's real name (e.g. when saving). SetHttpFileName handles non-ASCII
    // names by emitting the RFC 5987 filename* parameter when required.
    var disposition = new ContentDispositionHeaderValue("inline");
    disposition.SetHttpFileName(photo.FileName);
    httpContext.Response.Headers.ContentDisposition = disposition.ToString();

    // HEIC/HEIF cannot be natively decoded by most browsers; transcode to JPEG on the fly.
    if (transcoder.IsTranscodable(photo.FilePath))
    {
        var jpeg = await transcoder.TranscodeToJpegAsync(photo.FilePath, ct);
        return Results.Stream(jpeg, "image/jpeg");
    }

    var contentType = Path.GetExtension(photo.FilePath).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".gif"            => "image/gif",
        ".webp"           => "image/webp",
        ".bmp"            => "image/bmp",
        ".tiff" or ".tif" => "image/tiff",
        _                 => "application/octet-stream"
    };

    return Results.File(photo.FilePath, contentType);
});

app.MapGet("/api/slideshow/next", async (IPhotoService service) =>
{
    var page = await service.GetPhotosAsync(new PhotoFilter { Deduplicated = true, Page = 1, PageSize = int.MaxValue });
    if (page.TotalCount == 0)
        return Results.NotFound();

    var index = Random.Shared.Next(page.TotalCount);
    var picked = page.Items[index];

    // Enrich with sibling versions so the info overlay can list all copies.
    var enriched = await service.GetPhotoByIdAsync(picked.Id);
    return Results.Ok(enriched ?? picked);
});

app.MapGet("/api/config", (IOptions<PhotoOrganizerSettings> options) =>
    Results.Ok(options.Value));

// Reports whether the background index build is complete and how many photos are indexed.
// Useful for the UI to show a progress hint, and for integration tests to know when to assert.
app.MapGet("/api/index/status", (PhotoIndex index) =>
    Results.Ok(new { complete = index.IsComplete, count = index.Count }));

app.MapGet("/api/index/stats", (PhotoIndex index) =>
{
    var photos = index.SnapshotPhotos();
    var folders = index.SnapshotFolders();

    // Assign each photo to its nearest-ancestor source folder (mirrors the crawler's unit logic).
    var folderPathSet = folders.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var photoCounts = folders.ToDictionary(f => f.Path, _ => 0, StringComparer.OrdinalIgnoreCase);

    foreach (var photo in photos)
    {
        var dir = Path.GetDirectoryName(photo.FilePath) ?? string.Empty;
        var current = dir;
        while (!string.IsNullOrEmpty(current))
        {
            if (folderPathSet.Contains(current))
            {
                photoCounts[current]++;
                break;
            }
            var parent = Path.GetDirectoryName(current);
            if (parent == current) break;
            current = parent ?? string.Empty;
        }
    }

    // Sum sidecar file sizes for all known photos and folders without a full tree scan.
    long sidecarBytes = 0;
    foreach (var photo in photos)
    {
        var sidecarPath = photo.FilePath + ".meta.json";
        try { if (File.Exists(sidecarPath)) sidecarBytes += new FileInfo(sidecarPath).Length; }
        catch { /* best-effort */ }
    }
    foreach (var folder in folders)
    {
        var folderSidecar = Path.Combine(folder.Path, "_folder.json");
        try { if (File.Exists(folderSidecar)) sidecarBytes += new FileInfo(folderSidecar).Length; }
        catch { /* best-effort */ }
    }

    var dto = new IndexStatsDto
    {
        Complete = index.IsComplete,
        TotalPhotoCount = photos.Count,
        SidecarSizeBytes = sidecarBytes,
        Folders = folders
            .OrderBy(f => f.Label, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => new FolderStatsDto
            {
                Path = f.Path,
                Label = f.Label,
                Type = f.Type.ToString(),
                PhotoCount = photoCounts[f.Path]
            })
            .ToList()
    };

    return Results.Ok(dto);
});

app.MapPost("/api/crawler/start", async (HttpRequest httpRequest, [FromBody] StartCrawlRequest request, ICrawlerService service) =>
{
    // CSRF guard: require the custom header (forces a CORS preflight for cross-origin callers).
    if (string.IsNullOrWhiteSpace(httpRequest.Headers[CsrfHeader]))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    // Allowlist Mode and Step to prevent argument injection into the crawler process.
    var validationError = StartCrawlValidation.Validate(request);
    if (validationError is not null)
        return Results.BadRequest(validationError);

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
