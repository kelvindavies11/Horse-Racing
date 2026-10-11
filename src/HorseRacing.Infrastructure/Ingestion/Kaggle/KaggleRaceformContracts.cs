using System.Text.Json.Serialization;

namespace HorseRacing.Infrastructure.Ingestion.Kaggle;

public sealed record KaggleRaceformArchive(
    string ArchivePath,
    string DatabasePath,
    string ArchiveSha256,
    long ArchiveBytes);

public sealed record KaggleRaceformRawBatch(
    int SchemaVersion,
    string DatasetSlug,
    int DatasetVersion,
    string ArchiveEntry,
    string ArchiveSha256,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyCollection<KaggleRaceformRow> Rows);

public sealed record KaggleRaceformRow(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("course")] string Course,
    [property: JsonPropertyName("race_id")] long RaceId,
    [property: JsonPropertyName("off")] string? Off,
    [property: JsonPropertyName("race_name")] string? RaceName,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("class")] string? Class,
    [property: JsonPropertyName("pattern")] string? Pattern,
    [property: JsonPropertyName("rating_band")] string? RatingBand,
    [property: JsonPropertyName("age_band")] string? AgeBand,
    [property: JsonPropertyName("sex_rest")] string? SexRestriction,
    [property: JsonPropertyName("dist")] string? Distance,
    [property: JsonPropertyName("going")] string? Going,
    [property: JsonPropertyName("ran")] int? Ran,
    [property: JsonPropertyName("num")] int? Number,
    [property: JsonPropertyName("pos")] int? Position,
    [property: JsonPropertyName("draw")] int? Draw,
    [property: JsonPropertyName("ovr_btn")] decimal? OverallBeaten,
    [property: JsonPropertyName("btn")] decimal? Beaten,
    [property: JsonPropertyName("horse")] string? Horse,
    [property: JsonPropertyName("age")] int? Age,
    [property: JsonPropertyName("sex")] string? Sex,
    [property: JsonPropertyName("wgt")] string? Weight,
    [property: JsonPropertyName("hg")] string? Headgear,
    [property: JsonPropertyName("time")] string? Time,
    [property: JsonPropertyName("sp")] string? StartingPrice,
    [property: JsonPropertyName("jockey")] string? Jockey,
    [property: JsonPropertyName("trainer")] string? Trainer,
    [property: JsonPropertyName("prize")] long? Prize,
    [property: JsonPropertyName("or")] int? OfficialRating,
    [property: JsonPropertyName("rpr")] int? RacingPostRating,
    [property: JsonPropertyName("ts")] int? TopSpeed,
    [property: JsonPropertyName("sire")] string? Sire,
    [property: JsonPropertyName("dam")] string? Dam,
    [property: JsonPropertyName("damsire")] string? DamSire,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("comment")] string? Comment);
