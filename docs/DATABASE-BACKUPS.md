# Local database backups

The local PostgreSQL database is backed up independently of Git. Database archives must
never be committed to the repository. By default, the scripts store them in
`OneDrive\Backups\Horse-Racing`, outside this checkout, so OneDrive can copy them off the
computer.

## Backup policy

The installed schedule creates a compressed PostgreSQL custom-format archive every day
at 02:00 local time. The backup runs online and does not stop Raw collection or Curated
promotion. Each archive is checked immediately with `pg_restore --list` and accompanied
by a SHA-256 checksum and a small JSON manifest.

The rotation retains the union of:

- the 14 newest daily backups;
- one backup from each of the newest 8 ISO weeks; and
- one backup from each of the newest 12 calendar months.

On the first day of each month at 03:30, the latest archive is checksum-verified and
restored into an isolated, disposable PostgreSQL instance. The test checks for the
expected Raw and Curated tables, stops the isolated server, and removes its working
directory. Results are written to the backup directory under `restore-checks`. The live
database account does not need permission to create databases, and the live database is
never changed by the test.

## Install or refresh the schedule

The connection string must be stored in the Windows user environment variable
`ConnectionStrings__HorseRacing`. The installer does not copy the password into a task,
script, log, manifest, or repository file.

```powershell
powershell.exe -File scripts/install-database-backup-tasks.ps1
```

The installer creates these Windows scheduled tasks:

- `Horse Racing - Nightly Database Backup`
- `Horse Racing - Monthly Database Restore Check`

Both tasks use `StartWhenAvailable`, so Windows starts a missed run after the computer
becomes available. Overlapping copies of either task are ignored. They run in the
signed-in user's session, allowing the local OneDrive client to synchronize completed
archives without storing a Windows password in Task Scheduler.

To choose another location or time, rerun the installer. It safely replaces the task
definitions:

```powershell
powershell.exe -File scripts/install-database-backup-tasks.ps1 `
  -BackupDirectory 'D:\Backups\Horse-Racing' `
  -DailyBackupTime '01:30' `
  -MonthlyRestoreCheckTime '03:00'
```

## Run and verify manually

Create a backup immediately:

```powershell
powershell.exe -File scripts/backup-local-database.ps1
```

Restore and verify the latest archive immediately:

```powershell
powershell.exe -File scripts/test-database-backup.ps1
```

The restore check starts a temporary local PostgreSQL instance and never connects to or
overwrites the live `horse_racing` database. A successful check reports the restored Raw
collection-run and Curated object counts.

## Recovery

Use `pg_restore` to restore a selected `.dump` file into a newly created database first.
Check the restored website and counts before changing the application's connection
string. Do not restore over the live database while an import, API process, or website
is using it.

The custom archive contains the database schemas and data but not server-wide roles or
Windows environment variables. Recreate the local database account and connection
configuration separately when recovering onto a different computer.
