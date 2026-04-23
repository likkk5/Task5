using Microsoft.AspNetCore.Mvc;
using MusicStoreShowcase.Services;

namespace MusicStoreShowcase.Controllers;

[ApiController]
[Route("api/audio")]
public class AudioController : ControllerBase
{
    private readonly MusicGenerator _musicGen;
    public AudioController(MusicGenerator musicGen) => _musicGen = musicGen;

    [HttpGet]
    public IActionResult GetPreview(long seed)
    {
        var wav = _musicGen.GenerateWav(seed);
        return File(wav, "audio/wav");
    }
}