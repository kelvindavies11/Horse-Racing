[CmdletBinding()]
param(
    [string]$DatabaseHost = "127.0.0.1",
    [int]$Port = 5433,
    [string]$Database = "horse_racing",
    [string]$Username = "horse_racing",
    [string]$Password = "horse_racing_local"
)

$psql = Get-Command psql -ErrorAction Stop
$previousPassword = $env:PGPASSWORD
$env:PGPASSWORD = $Password

$sql = @'
WITH runner_results AS (
    SELECT
        domain_object_type,
        source_url,
        raw_payload_id,
        raw_collection_run_id,
        last_promotion_run_id,
        first_observed_at_utc,
        last_observed_at_utc,
        source_data -> 'foundData' AS data
    FROM curated.domain_objects
    WHERE domain_object_type = 'RunnerResult'
),
participant_observations AS (
    SELECT
        'Horse'::text AS domain_object_type,
        NULLIF(BTRIM(data ->> 'animalId'), '') AS source_key,
        NULLIF(BTRIM(data ->> 'racehorseName'), '') AS display_name,
        source_url,
        raw_payload_id,
        raw_collection_run_id,
        last_promotion_run_id,
        first_observed_at_utc,
        last_observed_at_utc,
        jsonb_strip_nulls(jsonb_build_object(
            'animalId', data -> 'animalId',
            'racehorseName', data -> 'racehorseName',
            'derivedFromResult', true)) AS found_data
    FROM runner_results
    UNION ALL
    SELECT
        'Jockey',
        NULLIF(BTRIM(data ->> 'jockeyId'), ''),
        NULLIF(BTRIM(data ->> 'jockeyName'), ''),
        source_url,
        raw_payload_id,
        raw_collection_run_id,
        last_promotion_run_id,
        first_observed_at_utc,
        last_observed_at_utc,
        jsonb_strip_nulls(jsonb_build_object(
            'jockeyId', data -> 'jockeyId',
            'jockeyName', data -> 'jockeyName',
            'jockeyLicenceType', data -> 'jockeyLicenceType',
            'derivedFromResult', true))
    FROM runner_results
    UNION ALL
    SELECT
        'Trainer',
        NULLIF(BTRIM(data ->> 'trainerId'), ''),
        NULLIF(BTRIM(data ->> 'trainerName'), ''),
        source_url,
        raw_payload_id,
        raw_collection_run_id,
        last_promotion_run_id,
        first_observed_at_utc,
        last_observed_at_utc,
        jsonb_strip_nulls(jsonb_build_object(
            'trainerId', data -> 'trainerId',
            'trainerName', data -> 'trainerName',
            'derivedFromResult', true))
    FROM runner_results
    UNION ALL
    SELECT
        'Owner',
        NULLIF(BTRIM(data ->> 'ownerId'), ''),
        NULLIF(BTRIM(data ->> 'ownerName'), ''),
        source_url,
        raw_payload_id,
        raw_collection_run_id,
        last_promotion_run_id,
        first_observed_at_utc,
        last_observed_at_utc,
        jsonb_strip_nulls(jsonb_build_object(
            'ownerId', data -> 'ownerId',
            'ownerName', data -> 'ownerName',
            'derivedFromResult', true))
    FROM runner_results
    UNION ALL
    SELECT
        'Stable',
        NULLIF(BTRIM(data ->> 'trainerId'), ''),
        CASE
            WHEN NULLIF(BTRIM(data ->> 'trainerName'), '') IS NULL THEN NULL
            ELSE 'Stable of ' || BTRIM(data ->> 'trainerName')
        END,
        source_url,
        raw_payload_id,
        raw_collection_run_id,
        last_promotion_run_id,
        first_observed_at_utc,
        last_observed_at_utc,
        jsonb_strip_nulls(jsonb_build_object(
            'trainerId', data -> 'trainerId',
            'trainerName', data -> 'trainerName',
            'stableName', CASE
                WHEN NULLIF(BTRIM(data ->> 'trainerName'), '') IS NULL THEN NULL
                ELSE 'Stable of ' || BTRIM(data ->> 'trainerName')
            END,
            'derivedFromResult', true,
            'identityBasis', 'Derived from the result''s trainer attribution; the BHA result does not provide an official stable name or location.'))
    FROM runner_results
),
ranked AS (
    SELECT
        participant_observations.*,
        MIN(first_observed_at_utc) OVER (
            PARTITION BY domain_object_type, source_key) AS earliest_observed_at_utc,
        MAX(last_observed_at_utc) OVER (
            PARTITION BY domain_object_type, source_key) AS latest_observed_at_utc,
        ROW_NUMBER() OVER (
            PARTITION BY domain_object_type, source_key
            ORDER BY last_observed_at_utc DESC, raw_payload_id DESC) AS observation_rank
    FROM participant_observations
    WHERE source_key IS NOT NULL AND display_name IS NOT NULL
),
upserted AS (
    INSERT INTO curated.domain_objects (
        id,
        source_system,
        domain_object_type,
        source_key,
        display_name,
        source_url,
        raw_payload_id,
        raw_collection_run_id,
        last_promotion_run_id,
        source_data,
        first_observed_at_utc,
        last_observed_at_utc)
    SELECT
        gen_random_uuid(),
        'BHA',
        domain_object_type,
        source_key,
        display_name,
        source_url,
        raw_payload_id,
        raw_collection_run_id,
        last_promotion_run_id,
        jsonb_build_object(
            'sourceSystem', 'BHA',
            'domainObjectType', domain_object_type,
            'sourceKey', source_key,
            'displayName', display_name,
            'foundData', found_data),
        earliest_observed_at_utc,
        latest_observed_at_utc
    FROM ranked
    WHERE observation_rank = 1
    ON CONFLICT (source_system, domain_object_type, source_key) DO UPDATE
    SET
        display_name = EXCLUDED.display_name,
        source_url = EXCLUDED.source_url,
        raw_payload_id = EXCLUDED.raw_payload_id,
        raw_collection_run_id = EXCLUDED.raw_collection_run_id,
        last_promotion_run_id = EXCLUDED.last_promotion_run_id,
        source_data = EXCLUDED.source_data,
        first_observed_at_utc = LEAST(
            curated.domain_objects.first_observed_at_utc,
            EXCLUDED.first_observed_at_utc),
        last_observed_at_utc = GREATEST(
            curated.domain_objects.last_observed_at_utc,
            EXCLUDED.last_observed_at_utc)
    WHERE curated.domain_objects.source_data -> 'foundData' ->> 'derivedFromResult' = 'true'
    RETURNING domain_object_type
)
SELECT domain_object_type, COUNT(*) AS rows_upserted
FROM upserted
GROUP BY domain_object_type
ORDER BY domain_object_type;
'@

try {
    $sql | & $psql.Source `
        --host=$DatabaseHost `
        --port=$Port `
        --username=$Username `
        --dbname=$Database `
        --no-password `
        --set=ON_ERROR_STOP=1

    if ($LASTEXITCODE -ne 0) {
        throw "Participant backfill failed with psql exit code $LASTEXITCODE."
    }
}
finally {
    if ($null -eq $previousPassword) {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
    else {
        $env:PGPASSWORD = $previousPassword
    }
}
