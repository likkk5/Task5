namespace MusicStoreShowcase.Services;

public class SeededRandom
{
    private readonly Random _random;

    public SeededRandom(long seed)
    {
        _random = new Random((int)(seed & 0xFFFFFFFF) ^ (int)(seed >> 32));
    }

    public int Next(int max) => _random.Next(max);
    public int Next(int min, int max) => _random.Next(min, max);
    public double NextDouble() => _random.NextDouble();
    public bool NextBool() => _random.NextDouble() < 0.5;
    public bool NextBool(double probability) => _random.NextDouble() < probability;

    public T Pick<T>(IList<T> list) => list[_random.Next(list.Count)];

    public float NextFloat(float min, float max)
    {
        return (float)(min + _random.NextDouble() * (max - min));
    }
}