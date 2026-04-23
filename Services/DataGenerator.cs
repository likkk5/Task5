using Bogus;
using MusicStoreShowcase.Models;
using System.Text.Json;

namespace MusicStoreShowcase.Services;

public class DataGenerator
{
    private readonly Dictionary<string, LanguageData> _languages = new();

    public DataGenerator(IWebHostEnvironment env)
    {
        LoadLanguage(env, "en-US");
        LoadLanguage(env, "ru-RU");
    }

    private void LoadLanguage(IWebHostEnvironment env, string culture)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", $"{culture}.json");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Missing language file: {path}");

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<LanguageData>(json);

        if (data == null)
            throw new Exception($"Failed to deserialize language file: {culture}");

        _languages[culture] = data;
    }

    public SongData GenerateSong(long userSeed, string culture, int index, float avgLikes)
    {
        long cultureSeed = GetStableCultureSeed(culture);
        long contentSeed = CombineSeeds(userSeed, cultureSeed, index, 0);
        long likesSeed = CombineSeeds(userSeed, cultureSeed, index, 1);

        var faker = new Faker();
        faker.Random = new Randomizer(ToIntSeed(contentSeed));

        var lang = _languages[culture];
        bool isEnglish = culture == "en-US";

        string artist = GenerateArtist(faker, lang, isEnglish);
        string title = GenerateTitle(faker, lang, culture, isEnglish);
        string album = GenerateAlbum(faker, lang, title, artist);
        string genre = GenerateGenre(faker, lang, isEnglish);

        var likesRng = new Random(ToIntSeed(likesSeed));
        int likes = ComputeLikes(likesRng, avgLikes);

        return new SongData
        {
            Index = index,
            Title = title,
            Artist = artist,
            Album = album,
            Genre = genre,
            Likes = likes,
            CoverSeed = contentSeed,
            AudioSeed = contentSeed
        };
    }

    private int ToIntSeed(long seed)
    {
        return (int)(seed ^ (seed >> 32));
    }

    private long GetStableCultureSeed(string culture)
    {
        long hash = 0;
        foreach (char c in culture)
            hash = hash * 31 + c;
        return hash;
    }

    private long CombineSeeds(params long[] seeds)
    {
        long result = 0;
        foreach (var s in seeds)
            result = (result * 6364136223846793005L) + s;
        return result;
    }

    private int ComputeLikes(Random rng, float avgLikes)
    {
        int floor = (int)Math.Floor(avgLikes);
        double frac = avgLikes - floor;
        return floor + (rng.NextDouble() < frac ? 1 : 0);
    }

    private string GenerateArtist(Faker faker, LanguageData lang, bool isEnglish)
    {
        if (isEnglish && faker.Random.Double() < 0.5)
        {
            return faker.Name.FullName();
        }

        if (faker.Random.Bool())
        {
            bool isFemale = faker.Random.Bool();
            string first = isFemale
                ? faker.PickRandom(lang.FirstNameFemale)
                : faker.PickRandom(lang.FirstNameMale);

            string last;
            if (!isEnglish && isFemale && lang.LastNameFemale.Length > 0)
            {
                last = faker.PickRandom(lang.LastNameFemale);
            }
            else
            {
                last = faker.PickRandom(lang.LastName);
            }
            return $"{first} {last}";
        }
        else
        {
            return $"{faker.PickRandom(lang.BandPrefix)} {faker.PickRandom(lang.BandSuffix)}";
        }
    }

    private string GenerateTitle(Faker faker, LanguageData lang, string culture, bool isEnglish)
    {
        List<string> patterns = new List<string>();

        if (culture == "ru-RU" && lang.SongAdjectiveFemale.Length > 0)
        {
            // Русские паттерны с учетом рода
            string noun = faker.PickRandom(lang.SongNoun);
            string adj = GetRussianAdjectiveByGender(faker, lang, noun);
            patterns.Add($"{adj} {noun}");

            string noun1 = faker.PickRandom(lang.SongNoun);
            string noun2 = faker.PickRandom(lang.SongNounGenitive);
            patterns.Add($"{noun1} {noun2}");

            string singleWord = faker.PickRandom(lang.SongNoun);
            patterns.Add(singleWord);
        }
        else
        {
            // Английские паттерны (без изменений)
            patterns.Add($"{faker.PickRandom(lang.SongAdjective)} {faker.PickRandom(lang.SongNoun)}");
            patterns.Add($"{faker.PickRandom(lang.SongNoun)} of {faker.PickRandom(lang.SongNoun)}");

            string singleWord = faker.Random.Word();
            if (singleWord.Length > 0)
            {
                singleWord = char.ToUpper(singleWord[0]) + singleWord.Substring(1);
                patterns.Add(singleWord);
            }

            string word1 = faker.Random.Word();
            string word2 = faker.Random.Word();
            if (word1.Length > 0) word1 = char.ToUpper(word1[0]) + word1.Substring(1);
            if (word2.Length > 0) word2 = char.ToUpper(word2[0]) + word2.Substring(1);
            patterns.Add($"{word1} {word2}");

            string w1 = faker.Random.Word();
            string w2 = faker.Random.Word();
            string w3 = faker.Random.Word();
            if (w1.Length > 0) w1 = char.ToUpper(w1[0]) + w1.Substring(1);
            if (w2.Length > 0) w2 = char.ToUpper(w2[0]) + w2.Substring(1);
            if (w3.Length > 0) w3 = char.ToUpper(w3[0]) + w3.Substring(1);
            patterns.Add($"{w1} {w2} {w3}");
        }

        return faker.PickRandom(patterns);
    }

    private string GetRussianAdjectiveByGender(Faker faker, LanguageData lang, string noun)
    {
        if (noun.EndsWith("а") || noun.EndsWith("я"))
            return faker.PickRandom(lang.SongAdjectiveFemale);
        else if (noun.EndsWith("о") || noun.EndsWith("е"))
            return faker.PickRandom(lang.SongAdjectiveNeuter);
        else
            return faker.PickRandom(lang.SongAdjective);
    }
    private string GenerateAlbum(Faker faker, LanguageData lang, string title, string artist)
    {
        if (faker.Random.Double() < 0.3)
            return lang.SingleIndicator;

        string template = faker.PickRandom(lang.AlbumTemplate);
        string part1 = faker.Random.Bool() ? title : artist;
        string part2 = faker.PickRandom(lang.SongNoun);

        return string.Format(template, part1, part2);
    }

    private string GenerateGenre(Faker faker, LanguageData lang, bool isEnglish)
    {
        if (isEnglish && faker.Random.Double() < 0.5)
        {
            return faker.Music.Genre();
        }

        return faker.PickRandom(lang.Genres);
    }
}