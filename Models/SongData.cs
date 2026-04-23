namespace MusicStoreShowcase.Models;

public record SongData
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public string Genre { get; init; } = "";
    public int Likes { get; init; }
    public long CoverSeed { get; init; }
    public long AudioSeed { get; init; }
}