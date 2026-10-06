using HorseRacing.Application.Ingestion.Raw;

namespace HorseRacing.Application.Ingestion.Results;

public interface IRaceResultsRawSourceClient : IRawSourceClient;

public interface IRaceResultsPayloadInterpreter
{
    Uri CreateRacecoursesUri();

    Uri CreateFixturePageUri(int year, int month, int page);

    Uri CreateFixtureRacesUri(ResultFixtureReference fixture);

    Uri CreateRaceResultsUri(ResultRaceReference race);

    ResultFixturePage ReadFixturePage(byte[] content);

    IReadOnlyCollection<ResultRaceReference> ReadRaces(byte[] content);
}

public sealed record ResultFixtureReference(
    int FixtureYear,
    int FixtureId,
    DateOnly FixtureDate,
    string CourseName);

public sealed record ResultRaceReference(
    int RaceYear,
    int RaceId,
    int DivisionSequence,
    string RaceName);

public sealed record ResultFixturePage(
    int CurrentPage,
    int LastPage,
    IReadOnlyCollection<ResultFixtureReference> Fixtures);

public sealed record CollectRaceResultsHistoryCommand(
    DateOnly FromDate,
    DateOnly ToDate,
    string CollectorVersion,
    TimeSpan DelayBetweenRequests);

public sealed record CollectRaceResultsHistoryResult(
    int FixturesFound,
    int RacesFound,
    int ResultPayloadsCollected,
    int FailedCollections,
    IReadOnlyCollection<string> SuccessfulJobNames);
