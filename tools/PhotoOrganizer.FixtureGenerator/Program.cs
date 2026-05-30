using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

var outputRoot = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "tests/fixtures/photos");

var originalsDir = Path.Combine(outputRoot, "originals");
var editsDir = Path.Combine(outputRoot, "edits");
Directory.CreateDirectory(originalsDir);
Directory.CreateDirectory(editsDir);

// Folder sidecars
WriteFolderJson(originalsDir, "Originals", "originals");
WriteFolderJson(editsDir, "Edits", "edits");

// 12 originals: IMG_1001.jpg … IMG_1012.jpg
// 3 of them also have edits: IMG_1001_edit.jpg, IMG_1005_edit.jpg, IMG_1009_edit.jpg
var baseDate = new DateTime(2020, 6, 15, 10, 0, 0);
var colors = new Rgba32[]
{
    new(220, 80, 80),   // red-ish
    new(80, 180, 80),   // green
    new(80, 100, 220),  // blue
    new(220, 180, 60),  // yellow
    new(180, 80, 180),  // magenta
    new(60, 200, 200),  // cyan
    new(200, 120, 60),  // orange
    new(100, 60, 180),  // purple
    new(180, 200, 80),  // lime
    new(60, 140, 180),  // steel blue
    new(220, 160, 160), // rose
    new(120, 200, 160), // mint
};

// GPS coordinates for 3 photos (degrees, minutes, seconds — Oslo, Norway area)
var gpsForIndices = new int[] { 0, 4, 8 };
var gpsCoords = new (double lat, double lon)[]
{
    (59.9139, 10.7522),
    (59.9, 10.8),
    (60.0, 10.5),
};

for (int i = 0; i < 12; i++)
{
    var name = $"IMG_{1001 + i}";
    var capturedAt = baseDate.AddDays(i * 30).AddHours(i);

    var gpsIndex = Array.IndexOf(gpsForIndices, i);
    (double lat, double lon)? gps = gpsIndex >= 0 ? gpsCoords[gpsIndex] : null;

    WriteJpeg(Path.Combine(originalsDir, $"{name}.jpg"), colors[i], capturedAt, gps);
}

// 3 edits — slightly different hue (brighter version of same color)
var editIndices = new[] { 0, 4, 8 };
foreach (var idx in editIndices)
{
    var name = $"IMG_{1001 + idx}_edit";
    var capturedAt = baseDate.AddDays(idx * 30).AddHours(idx);
    var r = colors[idx];
    var editColor = new Rgba32(Math.Min(r.R + 40, 255), Math.Min(r.G + 40, 255), Math.Min(r.B + 40, 255));
    WriteJpeg(Path.Combine(editsDir, $"{name}.jpg"), editColor, capturedAt, null);
}

Console.WriteLine($"Written fixtures to: {outputRoot}");
Console.WriteLine($"  originals/: 12 photos");
Console.WriteLine($"  edits/: 3 photos");
Console.WriteLine("Total: 15 photos, 3 duplicate groups");
return;

static void WriteFolderJson(string dir, string label, string type)
{
    var json = new { version = 1, label, type, enabled = true };
    var path = Path.Combine(dir, "_folder.json");
    File.WriteAllText(path, JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
}

static void WriteJpeg(string path, Rgba32 color, DateTime capturedAt, (double lat, double lon)? gps)
{
    using var image = new Image<Rgba32>(64, 64, color);

    var exif = new ExifProfile();
    exif.SetValue(ExifTag.DateTimeOriginal, capturedAt.ToString("yyyy:MM:dd HH:mm:ss"));

    if (gps.HasValue)
    {
        var (lat, lon) = gps.Value;
        exif.SetValue(ExifTag.GPSLatitudeRef, lat >= 0 ? "N" : "S");
        exif.SetValue(ExifTag.GPSLatitude, DecimalDegreesToRationals(Math.Abs(lat)));
        exif.SetValue(ExifTag.GPSLongitudeRef, lon >= 0 ? "E" : "W");
        exif.SetValue(ExifTag.GPSLongitude, DecimalDegreesToRationals(Math.Abs(lon)));
    }

    image.Metadata.ExifProfile = exif;
    image.SaveAsJpeg(path);
}

static Rational[] DecimalDegreesToRationals(double dd)
{
    var degrees = (uint)dd;
    var minutesFrac = (dd - degrees) * 60;
    var minutes = (uint)minutesFrac;
    var seconds = (minutesFrac - minutes) * 60;
    var secondsNum = (uint)(seconds * 100);
    return [new Rational(degrees, 1), new Rational(minutes, 1), new Rational(secondsNum, 100)];
}
