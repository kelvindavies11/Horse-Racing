using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HorseRacing.Api;

public static class ImportControlEndpoints
{
    public static IEndpointRouteBuilder MapImportControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin/imports");

        group.MapGet(
            "",
            async (ImportControlService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetSnapshotAsync(cancellationToken)));

        group.MapPost(
            "/{phaseId}/start",
            async (
                string phaseId,
                HttpContext context,
                ImportControlService service,
                CancellationToken cancellationToken) =>
            {
                var remoteAddress = context.Connection.RemoteIpAddress;
                if (remoteAddress is null || !IPAddress.IsLoopback(remoteAddress))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Import control is local-only.",
                        detail: "Start actions are accepted only from this computer.");
                }

                if (!string.Equals(
                        context.Request.Headers["X-Import-Control"],
                        "start",
                        StringComparison.Ordinal))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "The start confirmation header is missing.");
                }

                var result = await service.StartPhaseAsync(phaseId, cancellationToken);
                if (!result.Started)
                {
                    return Results.Conflict(new
                    {
                        title = "The import was not started.",
                        detail = result.Message,
                    });
                }

                return Results.Accepted(
                    $"/api/v1/admin/imports/{phaseId}",
                    new
                    {
                        phaseId,
                        processId = result.ProcessId,
                        message = result.Message,
                    });
            });

        return endpoints;
    }
}

public sealed class ImportControlService
{
    private const int MaximumRawWorkers = 5;
    private static readonly Regex MonthArgumentPattern = new(
        @"--from\s+(?<month>\d{4}-\d{2})-\d{2}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SupervisorStartMonthPattern = new(
        @"-StartMonth\s+""?(?<month>\d{4}-\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ThrottleLogPattern = new(
        @"^(?<timestamp>\S+)\s+BHA API is (?:still )?throttled \(HTTP (?<status>\d+)\); waiting (?<minutes>\d+) minutes?\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ShortRetryLogPattern = new(
        @"^(?<timestamp>\S+)\s+BHA API readiness probe returned (?<reason>.+); refreshing the public token and retrying in one minute\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions ProcessJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly ImportPhaseDefinition[] Definitions =
    [
        CreateYearPhase(2026, "A clean Raw-to-Curated pass for 2026, refreshed as new results become available."),
        CreateYearPhase(2024),
        CreateYearPhase(2023),
        CreateYearPhase(2022),
        CreateYearPhase(2021),
        CreateYearPhase(2020),
    ];

    private readonly ILogger<ImportControlService> logger;
    private readonly string repositoryRoot;
    private readonly SemaphoreSlim startLock = new(1, 1);

    public ImportControlService(
        IWebHostEnvironment environment,
        ILogger<ImportControlService> logger)
    {
        this.logger = logger;
        repositoryRoot = FindRepositoryRoot(environment.ContentRootPath);
    }

    public async Task<ImportControlSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var observedProcesses = await GetImportProcessesAsync(cancellationToken);
        var activeRawProcesses = observedProcesses
            .Where(process => process.CommandLine.Contains(
                "HorseRacing.RaceDataSync",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var activeProcess = activeRawProcesses.FirstOrDefault()
            ?? observedProcesses.FirstOrDefault(process => process.CommandLine.Contains(
                "HorseRacing.Bha.CuratedPromoter",
                StringComparison.OrdinalIgnoreCase));
        var activePhaseId = FindActivePhaseId(observedProcesses, activeProcess);
        var activeMonths = activeRawProcesses
            .Select(process => new { Process = process, Month = GetActiveMonth(process) })
            .Where(item => item.Month is not null)
            .GroupBy(item => item.Month!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Min(item => item.Process.StartedAtUtc),
                StringComparer.Ordinal);
        var activeMonth = activeMonths.Keys.OrderBy(month => month, StringComparer.Ordinal).FirstOrDefault();
        var activeStartedAtUtc = observedProcesses.Count == 0
            ? (DateTimeOffset?)null
            : observedProcesses.Min(process => process.StartedAtUtc);
        var isRunning = observedProcesses.Count > 0;
        var isExecuting = activeProcess is not null;
        var waitStatus = ReadWaitStatus(activePhaseId, isRunning, isExecuting);
        var phaseRows = Definitions.ToDictionary(
            definition => definition.Id,
            definition => ReadProgress(definition),
            StringComparer.Ordinal);
        var jobs = Definitions
            .SelectMany(definition => BuildJobs(
                definition,
                phaseRows[definition.Id],
                activePhaseId,
                activeMonths,
                isRunning,
                isExecuting))
            .ToArray();

        var phases = Definitions.Select(definition =>
        {
            var phaseJobs = jobs.Where(job => job.PhaseId == definition.Id).ToArray();
            var succeeded = phaseJobs.Count(job => job.Status == "Succeeded");
            var failed = phaseJobs.Count(job => job.Status == "Failed");
            var running = phaseJobs.Count(job => job.Status == "Running");
            var canStart = !isRunning;
            string? blocker = null;

            if (succeeded == phaseJobs.Length)
            {
                canStart = false;
                blocker = "Every month in this phase has succeeded.";
            }
            else if (isRunning)
            {
                canStart = false;
                blocker = "Another import phase owns the five-runner pool.";
            }

            var status = activePhaseId == definition.Id
                ? isExecuting ? "Running" : "Waiting"
                : succeeded == phaseJobs.Length
                    ? "Complete"
                    : failed > 0
                        ? "Needs attention"
                        : "Queued";

            return new ImportPhaseStatus(
                definition.Id,
                definition.Name,
                definition.Description,
                definition.StartMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                definition.EndMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                phaseJobs.Length,
                succeeded,
                failed,
                phaseJobs.Length - succeeded - failed - running,
                running,
                status,
                canStart,
                blocker);
        }).ToArray();

        return new ImportControlSnapshot(
            DateTimeOffset.UtcNow,
            isRunning,
            isExecuting ? "Running" : waitStatus?.State ?? (isRunning ? "Waiting" : "Idle"),
            waitStatus?.Message,
            waitStatus?.NextRetryAtUtc,
            activePhaseId,
            activeMonth,
            activeMonths.Keys.OrderBy(month => month, StringComparer.Ordinal).ToArray(),
            activeStartedAtUtc,
            observedProcesses.Count,
            activeRawProcesses.Length,
            MaximumRawWorkers,
            jobs.Length,
            jobs.Count(job => job.Status == "Succeeded"),
            jobs.Count(job => job.Status == "Failed"),
            jobs.Count(job => job.Status == "Queued"),
            phases,
            jobs);
    }

    public async Task<StartImportResult> StartPhaseAsync(
        string phaseId,
        CancellationToken cancellationToken)
    {
        await startLock.WaitAsync(cancellationToken);
        try
        {
            var definition = Definitions.FirstOrDefault(candidate => candidate.Id == phaseId);
            if (definition is null)
            {
                return new(false, "That import phase is not defined.", null);
            }

            var snapshot = await GetSnapshotAsync(cancellationToken);
            var phase = snapshot.Phases.First(candidate => candidate.Id == phaseId);
            if (!phase.CanStart)
            {
                return new(false, phase.StartBlocker ?? "This phase cannot be started yet.", null);
            }

            var starterPath = Path.Combine(repositoryRoot, "scripts", "start-race-data-import.ps1");
            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(starterPath);
            startInfo.ArgumentList.Add("-PhaseId");
            startInfo.ArgumentList.Add(phaseId);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new(false, "PowerShell could not be started.", null);
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = (await standardOutput).Trim();
            var error = (await standardError).Trim();

            if (process.ExitCode != 0 || !int.TryParse(output, out var processId))
            {
                logger.LogWarning(
                    "Import phase {PhaseId} was not started. Exit code {ExitCode}; {Error}",
                    phaseId,
                    process.ExitCode,
                    SanitiseProcessMessage(error));
                return new(
                    false,
                    string.IsNullOrWhiteSpace(error)
                        ? "The guarded starter did not return a process identifier."
                        : SanitiseProcessMessage(error),
                    null);
            }

            logger.LogInformation(
                "Started import phase {PhaseId} in process {ProcessId}.",
                phaseId,
                processId);
            return new(true, $"{definition.Name} was placed in the five-runner pool.", processId);
        }
        finally
        {
            startLock.Release();
        }
    }

    private IReadOnlyList<ImportProgressRow> ReadProgress(ImportPhaseDefinition definition)
    {
        var progressPath = Path.Combine(repositoryRoot, definition.StateDirectory, "progress.csv");
        if (!File.Exists(progressPath))
        {
            return [];
        }

        try
        {
            return File.ReadLines(progressPath)
                .Skip(1)
                .Select(ParseProgressRow)
                .Where(row => row is not null)
                .Cast<ImportProgressRow>()
                .ToArray();
        }
        catch (IOException exception)
        {
            logger.LogDebug(exception, "The import progress file was busy: {ProgressPath}", progressPath);
            return [];
        }
    }

    private ImportWaitStatus? ReadWaitStatus(
        string? activePhaseId,
        bool isRunning,
        bool isExecuting)
    {
        if (!isRunning || isExecuting || activePhaseId is null)
        {
            return null;
        }

        var definition = Definitions.FirstOrDefault(candidate => candidate.Id == activePhaseId);
        if (definition is null)
        {
            return null;
        }

        var logPath = Path.Combine(repositoryRoot, definition.StateDirectory, "resume.stdout.log");
        if (!File.Exists(logPath))
        {
            return null;
        }

        try
        {
            foreach (var line in ReadSharedLines(logPath).Reverse().Take(50))
            {
                var throttle = ThrottleLogPattern.Match(line);
                if (throttle.Success
                    && TryReadLogTimestamp(throttle, out var throttledAtUtc)
                    && int.TryParse(
                        throttle.Groups["minutes"].Value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var waitMinutes))
                {
                    var statusCode = throttle.Groups["status"].Value;
                    return new(
                        "Throttled",
                        $"BHA returned HTTP {statusCode}. Requests are paused to respect the upstream limit.",
                        throttledAtUtc.AddMinutes(waitMinutes));
                }

                var shortRetry = ShortRetryLogPattern.Match(line);
                if (shortRetry.Success && TryReadLogTimestamp(shortRetry, out var retryAtUtc))
                {
                    return new(
                        "Waiting",
                        $"BHA readiness returned {shortRetry.Groups["reason"].Value}.",
                        retryAtUtc.AddMinutes(1));
                }
            }
        }
        catch (IOException exception)
        {
            logger.LogDebug(exception, "The import resume log was busy: {LogPath}", logPath);
        }

        return null;
    }

    private static IReadOnlyList<string> ReadSharedLines(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static bool TryReadLogTimestamp(Match match, out DateTimeOffset timestamp) =>
        DateTimeOffset.TryParse(
            match.Groups["timestamp"].Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out timestamp);

    private static ImportProgressRow? ParseProgressRow(string line)
    {
        var fields = line.Split(',').Select(field => field.Trim().Trim('"')).ToArray();
        if (fields.Length < 6
            || !DateOnly.TryParseExact(
                fields[1],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var from)
            || !DateOnly.TryParseExact(
                fields[2],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var to))
        {
            return null;
        }

        var statusIndex = fields.Length >= 8 ? 5 : 3;
        var exitCodeIndex = fields.Length >= 8 ? 6 : 4;
        var completedAtIndex = fields.Length >= 8 ? 7 : 5;
        _ = int.TryParse(fields[exitCodeIndex], CultureInfo.InvariantCulture, out var exitCode);
        _ = DateTimeOffset.TryParse(
            fields[completedAtIndex],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var completedAtUtc);
        return new(from, to, fields[statusIndex], exitCode, completedAtUtc);
    }

    private static IEnumerable<ImportMonthJob> BuildJobs(
        ImportPhaseDefinition definition,
        IReadOnlyList<ImportProgressRow> rows,
        string? activePhaseId,
        IReadOnlyDictionary<string, DateTimeOffset> activeMonths,
        bool isImportRunning,
        bool isExecuting)
    {
        var waitingMonthAssigned = false;
        for (var month = definition.StartMonth; month <= definition.EndMonth; month = month.AddMonths(1))
        {
            var monthKey = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var attempts = rows.Where(row =>
                row.From.Year == month.Year && row.From.Month == month.Month).ToArray();
            var latest = attempts.LastOrDefault();
            var hasActiveWorker = activeMonths.TryGetValue(monthKey, out var monthStartedAtUtc);
            var isActive = activePhaseId == definition.Id && hasActiveWorker;
            var isWaiting = !waitingMonthAssigned
                && !isExecuting
                && isImportRunning
                && activePhaseId == definition.Id
                && (latest is null
                    || !string.Equals(latest.Status, "Succeeded", StringComparison.OrdinalIgnoreCase));
            waitingMonthAssigned = waitingMonthAssigned || isWaiting;
            var status = isActive
                ? "Running"
                : isWaiting
                    ? "Waiting"
                : latest is null
                    ? "Queued"
                    : string.Equals(latest.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
                        ? "Succeeded"
                        : "Failed";

            yield return new(
                $"{definition.Id}:{monthKey}",
                definition.Id,
                definition.Name,
                monthKey,
                month,
                month.AddMonths(1).AddDays(-1),
                status,
                attempts.Length,
                isActive ? monthStartedAtUtc : null,
                latest?.CompletedAtUtc,
                latest?.ExitCode);
        }
    }

    private static string? FindActivePhaseId(
        IReadOnlyList<ObservedImportProcess> processes,
        ObservedImportProcess? raceDataProcess)
    {
        var activeMonth = GetActiveMonth(raceDataProcess);
        if (activeMonth is not null
            && DateOnly.TryParseExact(
                $"{activeMonth}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var monthDate))
        {
            return Definitions.FirstOrDefault(definition =>
                monthDate >= definition.StartMonth && monthDate <= definition.EndMonth)?.Id;
        }

        var supervisor = processes.FirstOrDefault(process =>
            process.CommandLine.Contains("resume-available-race-data-tail.ps1", StringComparison.OrdinalIgnoreCase)
            || process.CommandLine.Contains("resume-monthly-race-data-history.ps1", StringComparison.OrdinalIgnoreCase));
        if (supervisor is null)
        {
            return null;
        }

        var startMonth = SupervisorStartMonthPattern.Match(supervisor.CommandLine).Groups["month"].Value;
        if (!DateOnly.TryParseExact(
                $"{startMonth}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var startDate))
        {
            return null;
        }

        return Definitions.FirstOrDefault(definition => definition.StartMonth == startDate)?.Id;
    }

    private static string? GetActiveMonth(ObservedImportProcess? process)
    {
        if (process is null)
        {
            return null;
        }

        var match = MonthArgumentPattern.Match(process.CommandLine);
        return match.Success ? match.Groups["month"].Value : null;
    }

    private async Task<IReadOnlyList<ObservedImportProcess>> GetImportProcessesAsync(
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return GetRaceDataProcessesFallback();
        }

        const string command = """
            $items = @(
                Get-CimInstance Win32_Process | Where-Object {
                    $isWorker = $_.Name -match '^dotnet(?:\.exe)?$' -and
                        ($_.CommandLine -match 'HorseRacing\.RaceDataSync' -or
                            $_.CommandLine -match 'HorseRacing\.Bha\.CuratedPromoter')
                    $isSupervisor = $_.Name -match '^pwsh(?:\.exe)?$' -and
                        $_.CommandLine -match '-File\s+.*(?:resume-after-throttle|resume-monthly-race-data-history|resume-available-race-data-tail)\.ps1(?:"|\s|$)'
                    $_.ProcessId -ne $PID -and
                    -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
                    ($isWorker -or $isSupervisor)
                } | ForEach-Object {
                    [pscustomobject]@{
                        ProcessId = $_.ProcessId
                        Name = $_.Name
                        CommandLine = $_.CommandLine
                        StartedAtUtc = $_.CreationDate.ToUniversalTime().ToString('O')
                    }
                }
            )
            ConvertTo-Json -InputObject $items -Compress
            """;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(command);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return GetRaceDataProcessesFallback();
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return GetRaceDataProcessesFallback();
            }

            return JsonSerializer.Deserialize<ObservedImportProcess[]>(output, ProcessJsonOptions) ?? [];
        }
        catch (Exception exception) when (
            exception is IOException
            or InvalidOperationException
            or JsonException
            or OperationCanceledException)
        {
            logger.LogDebug(exception, "Falling back to process-name import detection.");
            return GetRaceDataProcessesFallback();
        }
    }

    private static IReadOnlyList<ObservedImportProcess> GetRaceDataProcessesFallback()
    {
        return new[] { "HorseRacing.RaceDataSync", "HorseRacing.Bha.CuratedPromoter" }
            .SelectMany(Process.GetProcessesByName)
            .Select(process =>
            {
                using (process)
                {
                    DateTimeOffset startedAtUtc;
                    try
                    {
                        startedAtUtc = process.StartTime.ToUniversalTime();
                    }
                    catch (InvalidOperationException)
                    {
                        startedAtUtc = DateTimeOffset.UtcNow;
                    }

                    return new ObservedImportProcess(
                        process.Id,
                        process.ProcessName,
                        process.ProcessName,
                        startedAtUtc);
                }
            })
            .ToArray();
    }

    private static string FindRepositoryRoot(string contentRootPath)
    {
        for (var directory = new DirectoryInfo(contentRootPath); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HorseRacing.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The Horse Racing repository root could not be found.");
    }

    private static ImportPhaseDefinition CreateYearPhase(int year, string? description = null) =>
        new(
            $"year-{year}",
            $"{year} results",
            description ?? $"A restartable Raw-to-Curated archive pass for {year}.",
            new DateOnly(year, 1, 1),
            new DateOnly(year, 12, 1),
            $"artifacts/{year}-results-sync");

    private static string SanitiseProcessMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        var bearerIndex = message.IndexOf("Bearer ", StringComparison.OrdinalIgnoreCase);
        return bearerIndex < 0 ? message : message[..bearerIndex] + "Bearer [redacted]";
    }

    private sealed record ImportPhaseDefinition(
        string Id,
        string Name,
        string Description,
        DateOnly StartMonth,
        DateOnly EndMonth,
        string StateDirectory);

    private sealed record ImportProgressRow(
        DateOnly From,
        DateOnly To,
        string Status,
        int ExitCode,
        DateTimeOffset CompletedAtUtc);

    private sealed record ObservedImportProcess(
        int ProcessId,
        string Name,
        string CommandLine,
        DateTimeOffset StartedAtUtc);

    private sealed record ImportWaitStatus(
        string State,
        string Message,
        DateTimeOffset NextRetryAtUtc);
}

public sealed record ImportControlSnapshot(
    DateTimeOffset GeneratedAtUtc,
    bool IsImportRunning,
    string RunnerState,
    string? RunnerMessage,
    DateTimeOffset? NextRetryAtUtc,
    string? ActivePhaseId,
    string? ActiveMonth,
    IReadOnlyList<string> ActiveMonths,
    DateTimeOffset? ActiveStartedAtUtc,
    int ActiveProcessCount,
    int ActiveWorkerCount,
    int MaximumWorkerCount,
    int TotalMonths,
    int SucceededMonths,
    int FailedMonths,
    int QueuedMonths,
    IReadOnlyList<ImportPhaseStatus> Phases,
    IReadOnlyList<ImportMonthJob> Jobs);

public sealed record ImportPhaseStatus(
    string Id,
    string Name,
    string Description,
    string StartMonth,
    string EndMonth,
    int TotalMonths,
    int SucceededMonths,
    int FailedMonths,
    int QueuedMonths,
    int RunningMonths,
    string Status,
    bool CanStart,
    string? StartBlocker);

public sealed record ImportMonthJob(
    string Id,
    string PhaseId,
    string PhaseName,
    string Month,
    DateOnly From,
    DateOnly To,
    string Status,
    int Attempts,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int? ExitCode);

public sealed record StartImportResult(bool Started, string Message, int? ProcessId);
