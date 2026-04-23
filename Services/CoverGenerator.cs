using Microsoft.Extensions.Caching.Memory;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Net.Http;

namespace MusicStoreShowcase.Services;

public class CoverGenerator : ICoverGenerator
{
    private readonly List<Image<Rgba32>> _templates;
    private readonly ILogger<CoverGenerator> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly HttpClient _httpClient;

    private readonly string[] _availableFonts = new[]
    {
        "Liberation Sans",
        "Liberation Serif",
        "DejaVu Sans",
        "DejaVu Serif",
        "FreeSans"
    };

    private const double PROBABILITY_LOREM_PICSUM = 0.85;
    private const double PROBABILITY_TEMPLATE = 0.15;

    public CoverGenerator(IWebHostEnvironment env, ILogger<CoverGenerator> logger, IMemoryCache cache)
    {
        _env = env;
        _logger = logger;
        _cache = cache;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _templates = new List<Image<Rgba32>>();
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        var templatePath = System.IO.Path.Combine(_env.WebRootPath, "Templates", "Covers");

        if (!Directory.Exists(templatePath))
        {
            _logger.LogWarning("Templates folder not found: {Path}. Creating empty folder.", templatePath);
            Directory.CreateDirectory(templatePath);
            return;
        }

        var files = Directory.GetFiles(templatePath, "*.png");

        foreach (var file in files)
        {
            try
            {
                using var tempImage = Image.Load<Rgba32>(file);
                var image = new Image<Rgba32>(400, 400);
                image.Mutate(x => x.DrawImage(tempImage, 1));
                _templates.Add(image);
                _logger.LogInformation("Loaded template: {File}", System.IO.Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load template: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} templates from {Path}", _templates.Count, templatePath);
    }

    public async Task<byte[]> GenerateCoverAsync(long seed, string title, string artist, string genre)
    {
        var cacheKey = $"cover_{seed}_{title}_{artist}";

        if (_cache.TryGetValue(cacheKey, out byte[] cachedImage))
        {
            _logger.LogDebug("Cover loaded from cache for seed {Seed}, title {Title}", seed, title);
            return cachedImage;
        }

        _logger.LogInformation("Generating new cover for seed {Seed}, title {Title}", seed, title);

        var rng = new SeededRandom(seed);
        byte[] result;

        try
        {
            double roll = rng.NextDouble();

            if (roll < PROBABILITY_LOREM_PICSUM)
            {
                result = await GenerateFromLoremPicsum(seed, title, artist, rng);
            }
            else
            {
                if (_templates.Count > 0)
                {
                    result = GenerateFromTemplate(seed, title, artist, rng);
                }
                else
                {
                    result = await GenerateFromLoremPicsum(seed, title, artist, rng);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate cover for seed {Seed}, using template fallback if available", seed);

            if (_templates.Count > 0)
            {
                result = GenerateFromTemplate(seed, title, artist, new SeededRandom(seed));
            }
            else
            {
                throw;
            }
        }

        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
            Size = result.Length
        });

        return result;
    }

    private async Task<byte[]> GenerateFromLoremPicsum(long seed, string title, string artist, SeededRandom rng)
    {
        string baseUrl = $"https://picsum.photos/seed/{seed}/400/400";

        var filters = new List<string>();

        if (rng.NextBool(0.35)) filters.Add("grayscale");
        if (rng.NextBool(0.25)) filters.Add($"blur={rng.Next(1, 8)}");

        string url = filters.Any() ? $"{baseUrl}?{string.Join("&", filters)}" : baseUrl;

        _logger.LogDebug("Fetching image from: {Url}", url);

        byte[] imageBytes;
        try
        {
            imageBytes = await _httpClient.GetByteArrayAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch image from Lorem Picsum");
            throw;
        }

        using var image = Image.Load<Rgba32>(imageBytes);

        image.Mutate(ctx =>
        {
            if (rng.NextBool(0.25))
            {
                int cropW = rng.Next(320, 400);
                int cropH = rng.Next(320, 400);
                int x = rng.Next(0, 400 - cropW);
                int y = rng.Next(0, 400 - cropH);
                ctx.Crop(new Rectangle(x, y, cropW, cropH));
                ctx.Resize(400, 400);
            }

            if (rng.NextBool(0.45))
            {
                string textToDraw = rng.NextBool() ? title : artist;

                if (textToDraw.Length > 15)
                {
                    var words = textToDraw.Split(' ');
                    if (words.Length >= 2)
                    {
                        textToDraw = words[0] + " " + words[1];
                    }
                    if (textToDraw.Length > 15)
                    {
                        textToDraw = textToDraw.Substring(0, 12) + "...";
                    }
                }

                try
                {
                    string fontName = _availableFonts[rng.Next(_availableFonts.Length)];

                    var font = SystemFonts.CreateFont(fontName, 28, FontStyle.Bold);

                    var textColor = new Rgba32(
                        (byte)rng.Next(220, 255),
                        (byte)rng.Next(220, 255),
                        (byte)rng.Next(220, 255)
                    );

                    int x = 200 - (textToDraw.Length * 8) / 2; 
                    int offsetY = new[] { -30, 0, 30 }[rng.Next(0, 3)];
                    int y = 200 + offsetY;

                    x = Math.Max(10, Math.Min(x, 400 - textToDraw.Length * 8 - 10));
                    y = Math.Max(20, Math.Min(y, 370));

                    var textPosition = new Point(x, y);

                    ctx.DrawText(textToDraw, font, textColor, textPosition);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to draw text on cover");
                }
            }

            if (rng.NextBool(0.30))
            {
                var vignetteColor = new Rgba32(0, 0, 0, (byte)rng.Next(30, 100));
                var vignettePath = new EllipsePolygon(200, 200, 200);
                ctx.Fill(vignetteColor, vignettePath);
            }

            if (rng.NextBool(0.20))
            {
                var borderColor = new Rgba32(
                    (byte)rng.Next(50, 255),
                    (byte)rng.Next(50, 255),
                    (byte)rng.Next(50, 255)
                );
                var borderPath = new RectangularPolygon(5, 5, 390, 390);
                ctx.Draw(borderColor, rng.Next(3, 10), borderPath);
            }

            if (rng.NextBool(0.25))
            {
                var gradientColor = new Rgba32(
                    (byte)rng.Next(0, 100),
                    (byte)rng.Next(0, 100),
                    (byte)rng.Next(0, 100),
                    (byte)rng.Next(30, 80)
                );
                ctx.Fill(gradientColor);
            }
        });

        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        return ms.ToArray();
    }

    private byte[] GenerateFromTemplate(long seed, string title, string artist, SeededRandom rng)
    {
        var templateIndex = rng.Next(_templates.Count);
        var image = _templates[templateIndex].Clone(x => { });

        image.Mutate(ctx =>
        {
            if (rng.NextBool(0.35))
            {
                int brightness = rng.Next(-20, 20);
                ctx.Brightness(brightness / 100f);
            }

            if (rng.NextBool(0.20))
            {
                ctx.GaussianBlur(rng.Next(1, 3));
            }
            if (rng.NextBool(0.50))
            {
                string textToDraw = rng.NextBool() ? title : artist;

                if (textToDraw.Length > 15)
                {
                    var words = textToDraw.Split(' ');
                    if (words.Length >= 2)
                    {
                        textToDraw = words[0] + " " + words[1];
                    }
                    if (textToDraw.Length > 15)
                    {
                        textToDraw = textToDraw.Substring(0, 12) + "...";
                    }
                }
                try
                {
                    string fontName = _availableFonts[rng.Next(_availableFonts.Length)];
                    var font = SystemFonts.CreateFont(fontName, 26, FontStyle.Bold);

                    var textColor = new Rgba32(
                        (byte)rng.Next(200, 255),
                        (byte)rng.Next(200, 255),
                        (byte)rng.Next(200, 255)
                    );

                    int x = 200 - (textToDraw.Length * 7) / 2;
                    int offsetY = new[] { -30, 0, 30 }[rng.Next(0, 3)];
                    int y = 200 + offsetY;

                    x = Math.Max(10, Math.Min(x, 400 - textToDraw.Length * 7 - 10));
                    y = Math.Max(20, Math.Min(y, 370));

                    var textPosition = new Point(x, y);

                    ctx.DrawText(textToDraw, font, textColor, textPosition);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to draw text on template");
                }
            }
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        image.Dispose();

        return ms.ToArray();
    }

    public byte[] GenerateCover(long seed, string title, string artist, string genre)
    {
        return GenerateCoverAsync(seed, title, artist, genre).GetAwaiter().GetResult();
    }
}