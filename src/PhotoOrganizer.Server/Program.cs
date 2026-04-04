using Microsoft.AspNetCore.Mvc;
using PhotoOrganizer.Application.Crawler;
using PhotoOrganizer.Infrastructure.Crawler;
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors();
}

app.UseDefaultFiles();
app.UseStaticFiles();

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
