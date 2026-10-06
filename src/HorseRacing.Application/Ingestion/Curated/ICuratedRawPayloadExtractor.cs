namespace HorseRacing.Application.Ingestion.Curated;

public interface ICuratedRawPayloadExtractor
{
    CuratedRawPayloadExtraction Extract(RawPayloadForPromotion payload);
}
