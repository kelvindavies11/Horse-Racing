-- Run against a disposable migrated database: psql -v ON_ERROR_STOP=1 -f tests/sql/verify-domain.sql
-- All example data is rolled back, even after successful checks.
BEGIN;
DO $tests$
DECLARE
    course uuid := gen_random_uuid();
    yard uuid := gen_random_uuid();
    trainer uuid := gen_random_uuid();
    owner_id_value uuid := gen_random_uuid();
    jockey uuid := gen_random_uuid();
    horse1 uuid := gen_random_uuid();
    horse2 uuid := gen_random_uuid();
    meeting uuid := gen_random_uuid();
    race1 uuid := gen_random_uuid();
    race2 uuid := gen_random_uuid();
    runner1 uuid := gen_random_uuid();
    runner2 uuid := gen_random_uuid();
    foreign_runner uuid := gen_random_uuid();
    result1 uuid := gen_random_uuid();
    result2 uuid := gen_random_uuid();
BEGIN
    INSERT INTO racecourses VALUES (course, 'Verification Course', 'GB');
    INSERT INTO stables(id, name, town, country_code) VALUES (yard, 'Verification Yard', 'Newmarket', 'GB');
    INSERT INTO trainers(id, stable_id, name, country_code) VALUES (trainer, yard, 'Verification Trainer', 'GB');
    INSERT INTO jockeys(id, name, country_code) VALUES (jockey, 'Verification Jockey', 'GB');
    INSERT INTO jockeys(id, name, country_code) VALUES (gen_random_uuid(), 'Another unlicensed-reference rider', 'GB');
    INSERT INTO owners(id, display_name, type, country_code) VALUES (owner_id_value, 'Verification Syndicate', 'Syndicate', 'GB');
    INSERT INTO horses VALUES (horse1, 'Verification One', '2020-01-01', 'GB'), (horse2, 'Verification Two', '2020-01-02', 'GB');
    INSERT INTO meetings(id, racecourse_id, name, scheduled_date, type, status)
    VALUES (meeting, course, 'Verification Meeting', '2026-10-05', 'Flat', 'Scheduled');
    INSERT INTO races(id, meeting_id, race_number, name, scheduled_start_utc, code, surface, distance_metres, status)
    VALUES (race1, meeting, 1, 'First', '2026-10-05 14:00:00+00', 'Flat', 'Turf', 1609, 'Scheduled'),
           (race2, meeting, 2, 'Second', '2026-10-05 14:30:00+00', 'Flat', 'Turf', 1609, 'Scheduled');
    INSERT INTO runners(id, race_id, horse_id, trainer_id, owner_id, jockey_id, stable_id, cloth_number, draw, status)
    VALUES (runner1, race1, horse1, trainer, owner_id_value, jockey, yard, 1, 1, 'Declared'),
           (runner2, race1, horse2, trainer, owner_id_value, NULL, NULL, 2, 2, 'Declared'),
           (foreign_runner, race2, horse1, trainer, owner_id_value, jockey, yard, 1, 1, 'Declared');
    INSERT INTO race_results(id, race_id, published_at_utc, status)
    VALUES (result1, race1, now(), 'Provisional'), (result2, race2, now(), 'Provisional');

    BEGIN
        UPDATE races SET race_number = 1 WHERE id = race2;
        RAISE EXCEPTION 'Duplicate race number accepted';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
    BEGIN
        UPDATE runners SET cloth_number = 1 WHERE id = runner2;
        RAISE EXCEPTION 'Duplicate cloth accepted';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
    BEGIN
        UPDATE runners SET horse_id = horse1 WHERE id = runner2;
        RAISE EXCEPTION 'Duplicate horse accepted';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
    BEGIN
        UPDATE runners SET draw = 1 WHERE id = runner2;
        RAISE EXCEPTION 'Duplicate draw accepted';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
    BEGIN
        UPDATE runners SET declared_odds = 0.5 WHERE id = runner1;
        RAISE EXCEPTION 'Invalid decimal odds accepted';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        UPDATE runners SET status = 'NonRunner' WHERE id = runner2;
        RAISE EXCEPTION 'Non-runner without reason accepted';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO runner_results(id, race_result_id, race_id, runner_id, outcome, finish_position, is_dead_heat)
        VALUES (gen_random_uuid(), result1, race1, foreign_runner, 'Finished', 1, false);
        RAISE EXCEPTION 'Runner from another race accepted';
    EXCEPTION WHEN foreign_key_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO runner_results(id, race_result_id, race_id, runner_id, outcome, finish_position, is_dead_heat)
        VALUES (gen_random_uuid(), result2, race1, runner1, 'Finished', 1, false);
        RAISE EXCEPTION 'Result from another race accepted';
    EXCEPTION WHEN foreign_key_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO runner_results(id, race_result_id, race_id, runner_id, outcome, finish_position, is_dead_heat)
        VALUES (gen_random_uuid(), result1, race1, runner1, 'Fell', 1, false);
        RAISE EXCEPTION 'Non-finisher with position accepted';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        DELETE FROM trainers WHERE id = trainer;
        RAISE EXCEPTION 'Referenced trainer deletion accepted';
    EXCEPTION WHEN foreign_key_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO race_results(id, race_id, published_at_utc, status) VALUES (gen_random_uuid(), race1, now(), 'Provisional');
        RAISE EXCEPTION 'Second result accepted';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;

    -- Tied positions are valid SQL rows; aggregate publication validates their completeness.
    INSERT INTO runner_results(id, race_result_id, race_id, runner_id, outcome, finish_position, is_dead_heat)
    VALUES (gen_random_uuid(), result1, race1, runner1, 'Finished', 1, true),
           (gen_random_uuid(), result1, race1, runner2, 'Finished', 1, true);
    BEGIN
        INSERT INTO runner_results(id, race_result_id, race_id, runner_id, outcome, finish_position, is_dead_heat)
        VALUES (gen_random_uuid(), result1, race1, runner1, 'Finished', 2, false);
        RAISE EXCEPTION 'Duplicate runner result accepted';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
    IF (SELECT count(*) FROM runner_results rr
        JOIN runners r ON r.id = rr.runner_id AND r.race_id = rr.race_id
        JOIN races rc ON rc.id = rr.race_id
        JOIN meetings m ON m.id = rc.meeting_id
        JOIN racecourses c ON c.id = m.racecourse_id
        WHERE c.id = course) <> 2 THEN
        RAISE EXCEPTION 'Relationship traversal failed';
    END IF;
    RAISE NOTICE 'PASS: 12 rejection checks, dead heat, optional connections/licences and full relationship traversal';
END
$tests$;
ROLLBACK;
