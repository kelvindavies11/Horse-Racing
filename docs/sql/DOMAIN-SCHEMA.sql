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

