namespace MusicStoreShowcase.Services;

public interface ICoverGenerator
{
    Task<byte[]> GenerateCoverAsync(long seed, string title, string artist, string genre);
}