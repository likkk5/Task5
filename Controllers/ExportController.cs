using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using MusicStoreShowcase.Services;
using NAudio.Wave;
using NAudio.Lame;

namespace MusicStoreShowcase.Controllers;

[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private readonly DataGenerator _dataGen;
    private readonly MusicGenerator _musicGen;
    public ExportController(DataGenerator dataGen, MusicGenerator musicGen)
        => (_dataGen, _musicGen) = (dataGen, musicGen);

    [HttpPost]
    public async Task<IActionResult> ExportZip([FromBody] ExportRequest request)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var item in request.Songs)
            {
                var song = _dataGen.GenerateSong(item.Seed, request.Lang, item.Index, request.AvgLikes);
                var wavData = _musicGen.GenerateWav(song.AudioSeed);
                using var wavStream = new MemoryStream(wavData);
                using var reader = new WaveFileReader(wavStream);
                using var mp3Stream = new MemoryStream();
                using (var writer = new LameMP3FileWriter(mp3Stream, reader.WaveFormat, 128))
                {
                    await reader.CopyToAsync(writer);
                }
                mp3Stream.Position = 0;
                string safeFileName = $"{Sanitize(song.Title)} - {Sanitize(song.Artist)} - {Sanitize(song.Album)}.mp3";
                var entry = archive.CreateEntry(safeFileName);
                using var entryStream = entry.Open();
                await mp3Stream.CopyToAsync(entryStream);
            }
        }
        ms.Position = 0;
        return File(ms.ToArray(), "application/zip", "songs.zip");
    }

    private string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}

public class ExportRequest
{
    public string Lang { get; set; } = "";
    public float AvgLikes { get; set; }
    public List<SongExportItem> Songs { get; set; } = new();
}

public class SongExportItem
{
    public long Seed { get; set; }
    public int Index { get; set; }
}