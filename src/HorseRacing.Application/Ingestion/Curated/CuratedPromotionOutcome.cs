namespace HorseRacing.Application.Ingestion.Curated;

public enum CuratedPromotionOutcome
{
    Running = 0,
    Succeeded = 1,
    Skipped = 2,
    Failed = 3,
    Cancelled = 4
}
