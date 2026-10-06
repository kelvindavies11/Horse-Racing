CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE TABLE horses (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        foaled_on date NOT NULL,
        country_code character varying(2) NOT NULL,
        CONSTRAINT pk_horses PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE TABLE racecourses (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        country_code character varying(2) NOT NULL,
        CONSTRAINT pk_racecourses PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE TABLE races (
        id uuid NOT NULL,
        racecourse_id uuid NOT NULL,
        name character varying(200) NOT NULL,
        scheduled_start_utc timestamp with time zone NOT NULL,
        status character varying(20) NOT NULL,
        CONSTRAINT pk_races PRIMARY KEY (id),
        CONSTRAINT fk_races_racecourses_racecourse_id FOREIGN KEY (racecourse_id) REFERENCES racecourses (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE TABLE runners (
        id uuid NOT NULL,
        race_id uuid NOT NULL,
        horse_id uuid NOT NULL,
        cloth_number integer NOT NULL,
        declared_odds numeric(10,4),
        CONSTRAINT pk_runners PRIMARY KEY (id),
        CONSTRAINT fk_runners_horses_horse_id FOREIGN KEY (horse_id) REFERENCES horses (id) ON DELETE RESTRICT,
        CONSTRAINT fk_runners_races_race_id FOREIGN KEY (race_id) REFERENCES races (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE INDEX ix_horses_identity ON horses (name, foaled_on, country_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE UNIQUE INDEX ux_racecourses_name ON racecourses (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE INDEX ix_races_racecourse_start ON races (racecourse_id, scheduled_start_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE INDEX ix_runners_horse_id ON runners (horse_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE UNIQUE INDEX ux_runners_race_cloth_number ON runners (race_id, cloth_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    CREATE UNIQUE INDEX ux_runners_race_horse ON runners (race_id, horse_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005130000_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261005130000_InitialCreate', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    DO $guard$
    BEGIN
        IF EXISTS (SELECT 1 FROM races) OR EXISTS (SELECT 1 FROM runners) THEN
            RAISE EXCEPTION 'ExpandRacingDomain requires empty races and runners. Back up and design a source-backed legacy mapping before upgrading a populated scaffold.';
        END IF;
    END
    $guard$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races DROP CONSTRAINT fk_races_racecourses_racecourse_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races RENAME COLUMN racecourse_id TO meeting_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER INDEX ix_races_racecourse_start RENAME TO ix_races_meeting_start;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD carried_weight_pounds integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD draw integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD jockey_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD non_runner_reason character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD owner_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD stable_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD status character varying(20) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD trainer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD code character varying(30) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD distance_metres integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD going_description character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD race_number integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD surface character varying(20) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD CONSTRAINT ak_runners_id_race_id UNIQUE (id, race_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE TABLE jockeys (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        licence_number character varying(100),
        country_code character varying(2) NOT NULL,
        CONSTRAINT pk_jockeys PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE TABLE meetings (
        id uuid NOT NULL,
        racecourse_id uuid NOT NULL,
        name character varying(200) NOT NULL,
        scheduled_date date NOT NULL,
        type character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        CONSTRAINT pk_meetings PRIMARY KEY (id),
        CONSTRAINT ck_meetings_status CHECK (status IN ('Scheduled', 'InProgress', 'Completed', 'Abandoned')),
        CONSTRAINT ck_meetings_type CHECK (type IN ('Flat', 'Jump', 'Mixed')),
        CONSTRAINT fk_meetings_racecourses_racecourse_id FOREIGN KEY (racecourse_id) REFERENCES racecourses (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE TABLE owners (
        id uuid NOT NULL,
        display_name character varying(200) NOT NULL,
        type character varying(20) NOT NULL,
        country_code character varying(2) NOT NULL,
        CONSTRAINT pk_owners PRIMARY KEY (id),
        CONSTRAINT ck_owners_type CHECK (type IN ('Individual', 'Partnership', 'Syndicate', 'Company', 'RacingClub'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE TABLE race_results (
        id uuid NOT NULL,
        race_id uuid NOT NULL,
        published_at_utc timestamp with time zone NOT NULL,
        status character varying(20) NOT NULL,
        winning_time interval,
        CONSTRAINT pk_race_results PRIMARY KEY (id),
        CONSTRAINT ak_race_results_id_race_id UNIQUE (id, race_id),
        CONSTRAINT ck_race_results_status CHECK (status IN ('Provisional', 'Official')),
        CONSTRAINT ck_race_results_time CHECK (winning_time IS NULL OR winning_time > interval '0 seconds'),
        CONSTRAINT fk_race_results_races_race_id FOREIGN KEY (race_id) REFERENCES races (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE TABLE stables (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        town character varying(200) NOT NULL,
        country_code character varying(2) NOT NULL,
        CONSTRAINT pk_stables PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE TABLE runner_results (
        id uuid NOT NULL,
        race_result_id uuid NOT NULL,
        race_id uuid NOT NULL,
        runner_id uuid NOT NULL,
        outcome character varying(30) NOT NULL,
        finish_position integer,
        is_dead_heat boolean NOT NULL,
        distance_beaten_lengths numeric(8,3),
        starting_price_decimal numeric(10,4),
        prize_money numeric(14,2),
        CONSTRAINT pk_runner_results PRIMARY KEY (id),
        CONSTRAINT ck_runner_results_outcome CHECK (outcome IN ('Finished', 'PulledUp', 'Fell', 'UnseatedRider', 'Refused', 'BroughtDown', 'RanOut', 'Disqualified', 'Void')),
        CONSTRAINT ck_runner_results_position CHECK ((outcome = 'Finished' AND finish_position IS NOT NULL AND finish_position > 0) OR (outcome <> 'Finished' AND finish_position IS NULL AND NOT is_dead_heat)),
        CONSTRAINT ck_runner_results_values CHECK ((distance_beaten_lengths IS NULL OR distance_beaten_lengths >= 0) AND (starting_price_decimal IS NULL OR starting_price_decimal > 1) AND (prize_money IS NULL OR prize_money >= 0)),
        CONSTRAINT fk_runner_results_race_results_race_result_id FOREIGN KEY (race_result_id, race_id) REFERENCES race_results (id, race_id) ON DELETE CASCADE,
        CONSTRAINT fk_runner_results_runners_runner_id FOREIGN KEY (runner_id, race_id) REFERENCES runners (id, race_id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE TABLE trainers (
        id uuid NOT NULL,
        stable_id uuid,
        name character varying(200) NOT NULL,
        licence_number character varying(100),
        country_code character varying(2) NOT NULL,
        CONSTRAINT pk_trainers PRIMARY KEY (id),
        CONSTRAINT fk_trainers_stables_stable_id FOREIGN KEY (stable_id) REFERENCES stables (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX ix_runners_jockey_id ON runners (jockey_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX ix_runners_owner_id ON runners (owner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX "IX_runners_stable_id" ON runners (stable_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX ix_runners_trainer_id ON runners (trainer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE UNIQUE INDEX ux_runners_race_draw ON runners (race_id, draw) WHERE draw IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD CONSTRAINT ck_runners_numbers CHECK (cloth_number > 0 AND (draw IS NULL OR draw > 0) AND (carried_weight_pounds IS NULL OR carried_weight_pounds > 0));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD CONSTRAINT ck_runners_odds CHECK (declared_odds IS NULL OR declared_odds > 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD CONSTRAINT ck_runners_status CHECK ((status = 'Declared' AND non_runner_reason IS NULL) OR (status = 'NonRunner' AND length(trim(non_runner_reason)) > 0 AND non_runner_reason IS NOT NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE UNIQUE INDEX ux_races_meeting_race_number ON races (meeting_id, race_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD CONSTRAINT ck_races_code CHECK (code IN ('Flat', 'Hurdle', 'Steeplechase', 'NationalHuntFlat'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD CONSTRAINT ck_races_numbers CHECK (race_number > 0 AND distance_metres > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD CONSTRAINT ck_races_status CHECK (status IN ('Scheduled', 'Off', 'Finished', 'Abandoned'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD CONSTRAINT ck_races_surface CHECK (surface IN ('Turf', 'AllWeather'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE UNIQUE INDEX ux_jockeys_licence_number ON jockeys (country_code, licence_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX ix_meetings_racecourse_date_name ON meetings (racecourse_id, scheduled_date, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX ix_owners_identity ON owners (display_name, type, country_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE UNIQUE INDEX ux_race_results_race_id ON race_results (race_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX "IX_runner_results_race_result_id_race_id" ON runner_results (race_result_id, race_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX ix_runner_results_result_finish_position ON runner_results (race_result_id, finish_position);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE UNIQUE INDEX "IX_runner_results_runner_id_race_id" ON runner_results (runner_id, race_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE UNIQUE INDEX ux_runner_results_runner_id ON runner_results (runner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX ix_stables_identity ON stables (name, town, country_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE INDEX ix_trainers_stable_id ON trainers (stable_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    CREATE UNIQUE INDEX ux_trainers_licence_number ON trainers (country_code, licence_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE races ADD CONSTRAINT fk_races_meetings_meeting_id FOREIGN KEY (meeting_id) REFERENCES meetings (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD CONSTRAINT fk_runners_jockeys_jockey_id FOREIGN KEY (jockey_id) REFERENCES jockeys (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD CONSTRAINT fk_runners_owners_owner_id FOREIGN KEY (owner_id) REFERENCES owners (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD CONSTRAINT fk_runners_stables_stable_id FOREIGN KEY (stable_id) REFERENCES stables (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    ALTER TABLE runners ADD CONSTRAINT fk_runners_trainers_trainer_id FOREIGN KEY (trainer_id) REFERENCES trainers (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005140048_ExpandRacingDomain') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261005140048_ExpandRacingDomain', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005152258_AddRawIngestion') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'raw') THEN
            CREATE SCHEMA raw;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005152258_AddRawIngestion') THEN
    CREATE TABLE raw.collection_runs (
        id uuid NOT NULL,
        job_name character varying(100) NOT NULL,
        source_name character varying(200) NOT NULL,
        source_url character varying(2048) NOT NULL,
        collector_version character varying(50) NOT NULL,
        started_at_utc timestamp with time zone NOT NULL,
        completed_at_utc timestamp with time zone,
        outcome character varying(20) NOT NULL,
        http_status_code integer,
        error_code character varying(100),
        error_message character varying(2000),
        CONSTRAINT "PK_collection_runs" PRIMARY KEY (id),
        CONSTRAINT ck_raw_collection_runs_completion CHECK ((outcome = 'Running' AND completed_at_utc IS NULL) OR (outcome <> 'Running' AND completed_at_utc IS NOT NULL)),
        CONSTRAINT ck_raw_collection_runs_outcome CHECK (outcome IN ('Running', 'Succeeded', 'Failed', 'Cancelled'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005152258_AddRawIngestion') THEN
    CREATE TABLE raw.payloads (
        id uuid NOT NULL,
        collection_run_id uuid NOT NULL,
        source_url character varying(2048) NOT NULL,
        effective_url character varying(2048) NOT NULL,
        retrieved_at_utc timestamp with time zone NOT NULL,
        http_status_code integer NOT NULL,
        media_type character varying(200),
        character_encoding character varying(100),
        entity_tag character varying(500),
        last_modified_utc timestamp with time zone,
        sha256 character(64) NOT NULL,
        content_length bigint NOT NULL,
        content bytea NOT NULL,
        CONSTRAINT "PK_payloads" PRIMARY KEY (id),
        CONSTRAINT ck_raw_payloads_content_length CHECK (content_length = octet_length(content)),
        CONSTRAINT ck_raw_payloads_http_status_code CHECK (http_status_code BETWEEN 100 AND 599),
        CONSTRAINT ck_raw_payloads_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$'),
        CONSTRAINT "FK_payloads_collection_runs_collection_run_id" FOREIGN KEY (collection_run_id) REFERENCES raw.collection_runs (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005152258_AddRawIngestion') THEN
    CREATE INDEX ix_raw_collection_runs_source_started ON raw.collection_runs (source_url, started_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005152258_AddRawIngestion') THEN
    CREATE INDEX ix_raw_payloads_sha256 ON raw.payloads (sha256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005152258_AddRawIngestion') THEN
    CREATE UNIQUE INDEX ux_raw_payloads_collection_run ON raw.payloads (collection_run_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005152258_AddRawIngestion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261005152258_AddRawIngestion', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'curated') THEN
            CREATE SCHEMA curated;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE TABLE curated.promotion_runs (
        id uuid NOT NULL,
        job_name character varying(100) NOT NULL,
        promoter_version character varying(50) NOT NULL,
        raw_payload_id uuid NOT NULL,
        raw_collection_run_id uuid NOT NULL,
        source_job_name character varying(100) NOT NULL,
        source_name character varying(200) NOT NULL,
        source_url character varying(2048) NOT NULL,
        payload_sha256 character(64) NOT NULL,
        started_at_utc timestamp with time zone NOT NULL,
        completed_at_utc timestamp with time zone,
        outcome character varying(20) NOT NULL,
        records_found integer NOT NULL,
        records_upserted integer NOT NULL,
        error_code character varying(100),
        error_message character varying(2000),
        CONSTRAINT "PK_promotion_runs" PRIMARY KEY (id),
        CONSTRAINT ck_curated_promotion_runs_completion CHECK ((outcome = 'Running' AND completed_at_utc IS NULL) OR (outcome <> 'Running' AND completed_at_utc IS NOT NULL)),
        CONSTRAINT ck_curated_promotion_runs_outcome CHECK (outcome IN ('Running', 'Succeeded', 'Skipped', 'Failed', 'Cancelled')),
        CONSTRAINT ck_curated_promotion_runs_record_counts CHECK (records_found >= 0 AND records_upserted >= 0),
        CONSTRAINT fk_curated_promotion_runs_raw_collection_runs_raw_collection_run_id FOREIGN KEY (raw_collection_run_id) REFERENCES raw.collection_runs (id) ON DELETE RESTRICT,
        CONSTRAINT fk_curated_promotion_runs_raw_payloads_raw_payload_id FOREIGN KEY (raw_payload_id) REFERENCES raw.payloads (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE TABLE curated.domain_objects (
        id uuid NOT NULL,
        source_system character varying(50) NOT NULL,
        domain_object_type character varying(100) NOT NULL,
        source_key character varying(300) NOT NULL,
        display_name character varying(500) NOT NULL,
        source_url character varying(2048) NOT NULL,
        raw_payload_id uuid NOT NULL,
        raw_collection_run_id uuid NOT NULL,
        last_promotion_run_id uuid NOT NULL,
        source_data jsonb NOT NULL,
        first_observed_at_utc timestamp with time zone NOT NULL,
        last_observed_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_domain_objects" PRIMARY KEY (id),
        CONSTRAINT ck_curated_domain_objects_observation_dates CHECK (last_observed_at_utc >= first_observed_at_utc),
        CONSTRAINT ck_curated_domain_objects_source_data_json CHECK (jsonb_typeof(source_data) = 'object'),
        CONSTRAINT fk_curated_domain_objects_promotion_runs_last_promotion_run_id FOREIGN KEY (last_promotion_run_id) REFERENCES curated.promotion_runs (id) ON DELETE RESTRICT,
        CONSTRAINT fk_curated_domain_objects_raw_collection_runs_raw_collection_run_id FOREIGN KEY (raw_collection_run_id) REFERENCES raw.collection_runs (id) ON DELETE RESTRICT,
        CONSTRAINT fk_curated_domain_objects_raw_payloads_raw_payload_id FOREIGN KEY (raw_payload_id) REFERENCES raw.payloads (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE INDEX ix_curated_domain_objects_last_promotion_run_id ON curated.domain_objects (last_promotion_run_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE INDEX ix_curated_domain_objects_raw_collection_run_id ON curated.domain_objects (raw_collection_run_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE INDEX ix_curated_domain_objects_raw_payload_id ON curated.domain_objects (raw_payload_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE INDEX ix_curated_domain_objects_type_display_name ON curated.domain_objects (domain_object_type, display_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE UNIQUE INDEX ux_curated_domain_objects_source_identity ON curated.domain_objects (source_system, domain_object_type, source_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE INDEX ix_curated_promotion_runs_raw_collection_run_id ON curated.promotion_runs (raw_collection_run_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE INDEX ix_curated_promotion_runs_raw_payload_outcome ON curated.promotion_runs (raw_payload_id, outcome);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    CREATE INDEX ix_curated_promotion_runs_source_job_started ON curated.promotion_runs (source_job_name, started_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005181610_AddCuratedPromotion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261005181610_AddCuratedPromotion', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    CREATE TABLE curated.racecourse_locations (
        id uuid NOT NULL,
        source_system character varying(50) NOT NULL,
        source_course_key character varying(300) NOT NULL,
        course_name character varying(200) NOT NULL,
        postcode character varying(20),
        latitude numeric(9,6) NOT NULL,
        longitude numeric(9,6) NOT NULL,
        time_zone character varying(100) NOT NULL,
        location_source character varying(100) NOT NULL,
        source_url character varying(2048) NOT NULL,
        raw_payload_id uuid NOT NULL,
        raw_collection_run_id uuid NOT NULL,
        resolved_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_racecourse_locations" PRIMARY KEY (id),
        CONSTRAINT ck_curated_racecourse_locations_latitude CHECK (latitude BETWEEN -90 AND 90),
        CONSTRAINT ck_curated_racecourse_locations_longitude CHECK (longitude BETWEEN -180 AND 180),
        CONSTRAINT fk_curated_racecourse_locations_raw_collection_runs_raw_collection_run_id FOREIGN KEY (raw_collection_run_id) REFERENCES raw.collection_runs (id) ON DELETE RESTRICT,
        CONSTRAINT fk_curated_racecourse_locations_raw_payloads_raw_payload_id FOREIGN KEY (raw_payload_id) REFERENCES raw.payloads (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    CREATE TABLE curated.race_weather (
        id uuid NOT NULL,
        curated_race_id uuid NOT NULL,
        racecourse_location_id uuid NOT NULL,
        race_start_utc timestamp with time zone NOT NULL,
        weather_hour_utc timestamp with time zone NOT NULL,
        temperature_c numeric(5,2) NOT NULL,
        apparent_temperature_c numeric(5,2) NOT NULL,
        relative_humidity_percent integer NOT NULL,
        precipitation_millimetres numeric(8,2) NOT NULL,
        rain_millimetres numeric(8,2) NOT NULL,
        weather_code integer NOT NULL,
        wind_speed_kilometres_per_hour numeric(7,2) NOT NULL,
        wind_direction_degrees integer NOT NULL,
        wind_gust_kilometres_per_hour numeric(7,2) NOT NULL,
        source_url character varying(2048) NOT NULL,
        raw_payload_id uuid NOT NULL,
        raw_collection_run_id uuid NOT NULL,
        retrieved_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_race_weather" PRIMARY KEY (id),
        CONSTRAINT ck_curated_race_weather_code CHECK (weather_code BETWEEN 0 AND 99),
        CONSTRAINT ck_curated_race_weather_humidity CHECK (relative_humidity_percent BETWEEN 0 AND 100),
        CONSTRAINT ck_curated_race_weather_precipitation CHECK (precipitation_millimetres >= 0 AND rain_millimetres >= 0),
        CONSTRAINT ck_curated_race_weather_wind CHECK (wind_speed_kilometres_per_hour >= 0 AND wind_gust_kilometres_per_hour >= 0 AND wind_direction_degrees BETWEEN 0 AND 360),
        CONSTRAINT fk_curated_race_weather_domain_objects_curated_race_id FOREIGN KEY (curated_race_id) REFERENCES curated.domain_objects (id) ON DELETE RESTRICT,
        CONSTRAINT fk_curated_race_weather_locations_racecourse_location_id FOREIGN KEY (racecourse_location_id) REFERENCES curated.racecourse_locations (id) ON DELETE RESTRICT,
        CONSTRAINT fk_curated_race_weather_raw_collection_runs_raw_collection_run_id FOREIGN KEY (raw_collection_run_id) REFERENCES raw.collection_runs (id) ON DELETE RESTRICT,
        CONSTRAINT fk_curated_race_weather_raw_payloads_raw_payload_id FOREIGN KEY (raw_payload_id) REFERENCES raw.payloads (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    CREATE INDEX ix_curated_race_weather_location_start ON curated.race_weather (racecourse_location_id, race_start_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    CREATE INDEX ix_curated_race_weather_raw_collection_run_id ON curated.race_weather (raw_collection_run_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    CREATE INDEX ix_curated_race_weather_raw_payload_id ON curated.race_weather (raw_payload_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    CREATE UNIQUE INDEX ux_curated_race_weather_curated_race_id ON curated.race_weather (curated_race_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    CREATE INDEX ix_curated_racecourse_locations_raw_collection_run_id ON curated.racecourse_locations (raw_collection_run_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    CREATE INDEX ix_curated_racecourse_locations_raw_payload_id ON curated.racecourse_locations (raw_payload_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    CREATE UNIQUE INDEX ux_curated_racecourse_locations_source_identity ON curated.racecourse_locations (source_system, source_course_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006092937_AddRaceResultsWeather') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261006092937_AddRaceResultsWeather', '10.0.12');
    END IF;
END $EF$;
COMMIT;

