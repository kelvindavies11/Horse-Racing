using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Kaggle;

namespace HorseRacing.Infrastructure.Ingestion.Curated;

public sealed class RoutingCuratedRawPayloadExtractor(
    BhaCuratedDomainObjectExtractor bhaExtractor,
    KaggleRaceformCuratedExtractor kaggleExtractor)
    : ICuratedRawPayloadExtractor
{
    public CuratedRawPayloadExtraction Extract(RawPayloadForPromotion payload) =>
        kaggleExtractor.CanExtract(payload)
            ? kaggleExtractor.Extract(payload)
            : bhaExtractor.Extract(payload);
}
