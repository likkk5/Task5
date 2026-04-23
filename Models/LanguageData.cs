using System.Text.Json.Serialization;

public class LanguageData
{
    [JsonPropertyName("firstNameMale")]
    public string[] FirstNameMale { get; set; } = Array.Empty<string>();

    [JsonPropertyName("firstNameFemale")]
    public string[] FirstNameFemale { get; set; } = Array.Empty<string>();

    [JsonPropertyName("lastName")]
    public string[] LastName { get; set; } = Array.Empty<string>();

    [JsonPropertyName("bandPrefix")]
    public string[] BandPrefix { get; set; } = Array.Empty<string>();

    [JsonPropertyName("bandSuffix")]
    public string[] BandSuffix { get; set; } = Array.Empty<string>();

    [JsonPropertyName("songAdjective")]
    public string[] SongAdjective { get; set; } = Array.Empty<string>();

    [JsonPropertyName("songNoun")]
    public string[] SongNoun { get; set; } = Array.Empty<string>();

    [JsonPropertyName("albumTemplate")]
    public string[] AlbumTemplate { get; set; } = Array.Empty<string>();

    [JsonPropertyName("singleIndicator")]
    public string SingleIndicator { get; set; } = "";

    [JsonPropertyName("genres")]
    public string[] Genres { get; set; } = Array.Empty<string>();
}