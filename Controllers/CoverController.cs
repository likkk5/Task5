using Microsoft.AspNetCore.Mvc;
using MusicStoreShowcase.Services;

[ApiController]
[Route("api/cover")]
public class CoverController : ControllerBase
{
    private readonly ICoverGenerator _coverGen;

    public CoverController(ICoverGenerator coverGen)
    {
        _coverGen = coverGen;
    }

    [HttpGet]
    public async Task<IActionResult> GetCover(long seed, string title, string artist, string genre)
    {
        var png = await _coverGen.GenerateCoverAsync(seed, title, artist, genre);
        return File(png, "image/png");
    }
}