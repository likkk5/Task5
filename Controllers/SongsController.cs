using Microsoft.AspNetCore.Mvc;
using MusicStoreShowcase.Services;
using MusicStoreShowcase.Models;

namespace MusicStoreShowcase.Controllers;

[ApiController]
[Route("api/songs")]
public class SongsController : ControllerBase
{
    private readonly DataGenerator _dataGen;
    public SongsController(DataGenerator dataGen) => _dataGen = dataGen;

    [HttpGet]
    public IActionResult GetSongs(
        string lang = "en-US",
        long seed = 12345,
        int page = 1,
        int pageSize = 15,
        float avgLikes = 5.0f)
    {
        var songs = new List<SongData>();
        int start = (page - 1) * pageSize;
        for (int i = 0; i < pageSize; i++)
        {
            int recordIndex = start + i + 1;
            songs.Add(_dataGen.GenerateSong(seed, lang, recordIndex, avgLikes));
        }
        return Ok(songs);
    }
}