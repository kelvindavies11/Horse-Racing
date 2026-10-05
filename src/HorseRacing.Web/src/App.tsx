import { useMemo, useState, type ReactNode, type SVGProps } from "react";
import type { MeetingSummary, RaceSummary } from "./domain/racing";
import { useRaceDay } from "./hooks/useRaceDay";

type MeetingFilter = "All" | "Flat" | "Jump";

const Icon = ({ children, ...props }: SVGProps<SVGSVGElement> & { children: ReactNode }) => (
  <svg
    aria-hidden="true"
    fill="none"
    viewBox="0 0 24 24"
    stroke="currentColor"
    strokeWidth="1.8"
    strokeLinecap="round"
    strokeLinejoin="round"
    {...props}
  >
    {children}
  </svg>
);

const GridIcon = () => (
  <Icon><rect x="3" y="3" width="7" height="7" rx="2" /><rect x="14" y="3" width="7" height="7" rx="2" /><rect x="3" y="14" width="7" height="7" rx="2" /><rect x="14" y="14" width="7" height="7" rx="2" /></Icon>
);
const CalendarIcon = () => (
  <Icon><rect x="3" y="5" width="18" height="16" rx="3" /><path d="M8 3v4M16 3v4M3 10h18" /></Icon>
);
const ClockIcon = () => (
  <Icon><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></Icon>
);
const FlagIcon = () => (
  <Icon><path d="M5 21V4m0 1c5-3 7 3 14 0v10c-7 3-9-3-14 0" /></Icon>
);
const SearchIcon = () => (
  <Icon><circle cx="11" cy="11" r="7" /><path d="m20 20-4-4" /></Icon>
);
const RefreshIcon = () => (
  <Icon><path d="M20 7v5h-5M4 17v-5h5" /><path d="M6.1 8.1A7.5 7.5 0 0 1 19.5 12M4.5 12a7.5 7.5 0 0 0 13.4 3.9" /></Icon>
);
const ChevronIcon = ({ direction = "right" }: { direction?: "left" | "right" }) => (
  <Icon className={direction === "left" ? "chevron chevron--left" : "chevron"}><path d="m9 18 6-6-6-6" /></Icon>
);
const PinIcon = () => (
  <Icon><path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z" /><circle cx="12" cy="10" r="2.5" /></Icon>
);
const CloudIcon = () => (
  <Icon><path d="M17.5 19H7a5 5 0 1 1 1.4-9.8A6 6 0 0 1 20 11.5 3.75 3.75 0 0 1 17.5 19Z" /></Icon>
);
const DatabaseIcon = () => (
  <Icon><ellipse cx="12" cy="5" rx="8" ry="3" /><path d="M4 5v7c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12v7c0 1.7 3.6 3 8 3s8-1.3 8-3v-7" /></Icon>
);

const toIsoDate = (date: Date) => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
};

const shiftDate = (date: string, days: number) => {
  const value = new Date(`${date}T12:00:00`);
  value.setDate(value.getDate() + days);
  return toIsoDate(value);
};

const formatLongDate = (date: string) =>
  new Intl.DateTimeFormat("en-GB", {
    weekday: "long",
    day: "numeric",
    month: "long",
    timeZone: "Europe/London",
  }).format(new Date(`${date}T12:00:00Z`));

const formatShortDate = (date: string) =>
  new Intl.DateTimeFormat("en-GB", {
    weekday: "short",
    day: "numeric",
    month: "short",
    timeZone: "Europe/London",
  }).format(new Date(`${date}T12:00:00Z`));

const formatDayNumber = (date: string) => date.slice(-2);

const formatMonthCode = (date: string) =>
  new Intl.DateTimeFormat("en-GB", { month: "short", timeZone: "Europe/London" })
    .format(new Date(`${date}T12:00:00Z`))
    .toUpperCase();

const formatYearShort = (date: string) => date.slice(2, 4);

const formatTime = (date: string) =>
  new Intl.DateTimeFormat("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "Europe/London",
  }).format(new Date(date));

const formatUpdated = (date: string) =>
  new Intl.DateTimeFormat("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "Europe/London",
  }).format(new Date(date));

const formatDistance = (metres: number) => {
  const miles = metres / 1609.344;
  if (miles < 1) return `${(miles * 8).toFixed(0)}f`;
  const whole = Math.floor(miles);
  const furlongs = Math.round((miles - whole) * 8);
  return furlongs === 0 ? `${whole}m` : `${whole}m ${furlongs}f`;
};

const displayCode = (code: RaceSummary["code"]) => {
  if (code === "Steeplechase") return "Chase";
  if (code === "NationalHuntFlat") return "NH Flat";
  return code;
};

const getFirstScheduledRace = (meetings: MeetingSummary[]) =>
  meetings
    .flatMap((meeting) => meeting.races.map((race) => ({ meeting, race })))
    .filter(({ race }) => race.status === "Scheduled")
    .sort((a, b) => a.race.scheduledStartUtc.localeCompare(b.race.scheduledStartUtc))[0];

function App() {
  const [selectedDate, setSelectedDate] = useState(() => toIsoDate(new Date()));
  const [filter, setFilter] = useState<MeetingFilter>("All");
  const [query, setQuery] = useState("");
  const { data, error, isLoading, reload } = useRaceDay(selectedDate);

  const filteredMeetings = useMemo(() => {
    const searchTerm = query.trim().toLowerCase();
    return (data?.meetings ?? []).filter((meeting) => {
      const matchesType = filter === "All" || meeting.type === filter || meeting.type === "Mixed";
      const matchesSearch =
        !searchTerm ||
        meeting.racecourse.name.toLowerCase().includes(searchTerm) ||
        meeting.name.toLowerCase().includes(searchTerm) ||
        meeting.races.some((race) => race.name.toLowerCase().includes(searchTerm));
      return matchesType && matchesSearch;
    });
  }, [data, filter, query]);

  const allRaces = data?.meetings.flatMap((meeting) => meeting.races) ?? [];
  const runnerCount = allRaces.reduce((total, race) => total + race.runnerCount, 0);
  const nextRace = data ? getFirstScheduledRace(data.meetings) : undefined;
  const completedRaces = allRaces.filter((race) => race.status === "Finished").length;

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <a className="brand" href="#overview" aria-label="Paddock home">
          <span className="brand__mark"><span>P</span></span>
          <span><strong>Paddock</strong><small>Race office / 01</small></span>
        </a>

        <nav className="primary-nav" aria-label="Primary navigation">
          <p className="nav-label">Race office</p>
          <a className="nav-item nav-item--active" href="#overview"><GridIcon />Overview</a>
          <a className="nav-item" href="#meetings"><CalendarIcon />Meetings</a>
          <a className="nav-item" href="#schedule"><ClockIcon />Race schedule</a>
        </nav>

        <div className="sidebar__footer">
          <div className="source-card">
            <span className="source-card__icon"><DatabaseIcon /></span>
            <div><strong>Local workspace</strong><span>API-ready foundation</span></div>
          </div>
          <p>FIELD SYSTEM // BRITISH RACING<br />LOCAL SIGNAL ACTIVE</p>
        </div>
      </aside>

      <main className="main-content" id="overview">
        <header className="topbar">
          <div className="mobile-brand"><span className="brand__mark"><span>P</span></span><strong>Paddock</strong></div>
          <div className="topbar__context">
            <span className="eyebrow">DAYBOOK / UK</span>
            <span className="topbar__separator" />
            <span>Trackside data system</span>
          </div>
          <div className="topbar__actions">
            {data && (
              <span className={`data-badge data-badge--${data.origin}`}>
                <span className="status-dot" />
                {data.origin === "api" ? "Live API" : data.origin === "fallback" ? "Fallback data" : "Demo data"}
              </span>
            )}
            <button className="icon-button" type="button" onClick={reload} aria-label="Refresh racing data">
              <RefreshIcon />
            </button>
            <div className="avatar" aria-label="Local user">KL</div>
          </div>
        </header>

        <div className="page-container">
          <section className="page-heading">
            <div>
              <p className="overline">Race day transmission // 001</p>
              <h1>Race day,<br /><em>without the blinkers.</em></h1>
              <p className="heading-copy">The whole card in one uncompromising field note—meetings, runners, going and every off time.</p>
            </div>
            <div className="hero-tools">
              <div className="race-ticket" aria-hidden="true">
                <span className="race-ticket__label">ADMIT / DATA</span>
                <strong>{formatDayNumber(selectedDate)}</strong>
                <span className="race-ticket__date">{formatMonthCode(selectedDate)}<br />’{formatYearShort(selectedDate)}</span>
                <span className="race-ticket__barcode" />
              </div>
              <div className="date-control" aria-label="Selected race date">
                <button type="button" aria-label="Previous day" onClick={() => setSelectedDate(shiftDate(selectedDate, -1))}>
                  <ChevronIcon direction="left" />
                </button>
                <div><CalendarIcon /><span><small>Race date</small><strong>{formatShortDate(selectedDate)}</strong></span></div>
                <button type="button" aria-label="Next day" onClick={() => setSelectedDate(shiftDate(selectedDate, 1))}>
                  <ChevronIcon />
                </button>
              </div>
            </div>
          </section>

          {isLoading && <DashboardSkeleton />}

          {!isLoading && error && (
            <section className="state-panel" role="alert">
              <span className="state-panel__icon"><FlagIcon /></span>
              <div><p className="overline">Connection issue</p><h2>Racing data couldn’t be loaded.</h2><p>{error}</p></div>
              <button className="button button--primary" type="button" onClick={reload}>Try again</button>
            </section>
          )}

          {!isLoading && data && (
            <>
              {data.notice && (
                <div className="notice" role="status">
                  <span className="notice__dot" />
                  <span>{data.notice}</span>
                  <small>Updated {formatUpdated(data.generatedAtUtc)}</small>
                </div>
              )}

              <section className="overview-grid" aria-label="Race day summary">
                <div className="metrics-card">
                  <Metric label="Meetings" value={data.meetings.length} note={`${data.meetings.filter((meeting) => meeting.type === "Flat").length} flat · ${data.meetings.filter((meeting) => meeting.type === "Jump").length} jumps`} icon={<PinIcon />} />
                  <Metric label="Races" value={allRaces.length} note={`${completedRaces} resulted`} icon={<FlagIcon />} />
                  <Metric label="Declared runners" value={runnerCount} note="Across today’s cards" icon={<GridIcon />} />
                </div>

                {nextRace && <NextRaceCard meeting={nextRace.meeting} race={nextRace.race} />}
              </section>

              <section className="content-section" id="meetings">
                <div className="section-heading">
                  <div><p className="overline">The national card / 04 posts</p><h2>Today’s meetings</h2></div>
                  <div className="filters">
                    <div className="segmented-control" aria-label="Filter meeting type">
                      {(["All", "Flat", "Jump"] as const).map((option) => (
                        <button key={option} type="button" aria-pressed={filter === option} onClick={() => setFilter(option)}>{option}</button>
                      ))}
                    </div>
                    <label className="search-field">
                      <span className="sr-only">Search meetings or races</span>
                      <SearchIcon />
                      <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search racecourse" />
                    </label>
                  </div>
                </div>

                {filteredMeetings.length > 0 ? (
                  <div className="meeting-grid">
                    {filteredMeetings.map((meeting) => <MeetingCard key={meeting.id} meeting={meeting} />)}
                  </div>
                ) : (
                  <div className="empty-state">
                    <SearchIcon /><h3>No meetings match those filters</h3><p>Try another race type or clear your search.</p>
                    <button type="button" className="text-button" onClick={() => { setFilter("All"); setQuery(""); }}>Clear filters</button>
                  </div>
                )}
              </section>

              <section className="content-section schedule-section" id="schedule">
                <div className="section-heading">
                  <div><p className="overline">Off-time transmission</p><h2>Race schedule</h2></div>
                  <span className="section-meta">Times shown in UK local time</span>
                </div>
                <RaceSchedule meetings={filteredMeetings} />
              </section>

              <footer className="page-footer">
                <span>PADDOCK® / LOCAL RACE OFFICE</span>
                <span>{formatLongDate(data.date)} · {data.meetings.length} meetings loaded</span>
              </footer>
            </>
          )}
        </div>
      </main>
    </div>
  );
}

const Metric = ({ label, value, note, icon }: { label: string; value: number; note: string; icon: ReactNode }) => (
  <div className="metric">
    <span className="metric__icon">{icon}</span>
    <div><span>{label}</span><strong>{value}</strong><small>{note}</small></div>
  </div>
);

const NextRaceCard = ({ meeting, race }: { meeting: MeetingSummary; race: RaceSummary }) => (
  <article className="next-race">
    <div className="next-race__rings" aria-hidden="true"><span /><span /><span /></div>
    <div className="next-race__top"><span className="live-pill"><span />On deck / next off</span><span className="race-stamp">R{race.raceNumber}</span></div>
    <p>{meeting.racecourse.name} · Race {race.raceNumber}</p>
    <div className="next-race__main">
      <strong>{formatTime(race.scheduledStartUtc)}</strong>
      <div><h2>{race.name}</h2><span>{displayCode(race.code)} · {formatDistance(race.distanceMetres)} · {race.runnerCount} runners</span></div>
    </div>
    <div className="next-race__footer"><span>{meeting.goingDescription}</span><span>{meeting.weatherDescription}</span></div>
  </article>
);

const MeetingCard = ({ meeting }: { meeting: MeetingSummary }) => {
  const next = meeting.races.find((race) => race.status === "Scheduled") ?? meeting.races[0];
  const times = meeting.races.slice(0, 5);

  return (
    <article className="meeting-card" data-testid="meeting-card">
      <div className="meeting-card__header">
        <div className="course-monogram" aria-hidden="true">{meeting.racecourse.name.charAt(0)}</div>
        <div><span className="meeting-card__type">{meeting.type} meeting</span><h3>{meeting.racecourse.name}</h3><p><PinIcon />{meeting.racecourse.locality}</p></div>
        <span className={`meeting-status meeting-status--${meeting.status.toLowerCase()}`}>{meeting.status === "InProgress" ? "In progress" : meeting.status}</span>
      </div>
      <div className="meeting-card__details">
        <span><CloudIcon />{meeting.weatherDescription}</span>
        <span><FlagIcon />{meeting.goingDescription}</span>
      </div>
      <div className="race-time-row" aria-label={`${meeting.racecourse.name} race times`}>
        {times.map((race) => (
          <span key={race.id} className={race.id === next?.id ? "race-time race-time--next" : race.status === "Finished" ? "race-time race-time--finished" : "race-time"}>
            <small>R{race.raceNumber}</small>{formatTime(race.scheduledStartUtc)}
          </span>
        ))}
      </div>
      <div className="meeting-card__footer"><span>{meeting.races.length} races</span><span>{meeting.races.reduce((sum, race) => sum + race.runnerCount, 0)} declared runners</span></div>
    </article>
  );
};

const RaceSchedule = ({ meetings }: { meetings: MeetingSummary[] }) => {
  const entries = meetings
    .flatMap((meeting) => meeting.races.map((race) => ({ meeting, race })))
    .sort((a, b) => a.race.scheduledStartUtc.localeCompare(b.race.scheduledStartUtc));

  if (entries.length === 0) return <div className="schedule-empty">No races to show for the current filters.</div>;

  return (
    <div className="schedule-table" role="table" aria-label="Race schedule">
      <div className="schedule-row schedule-row--head" role="row">
        <span role="columnheader">Off</span><span role="columnheader">Race</span><span role="columnheader">Course</span><span role="columnheader">Type</span><span role="columnheader">Runners</span><span role="columnheader">Status</span>
      </div>
      {entries.map(({ meeting, race }) => (
        <div className="schedule-row" role="row" key={race.id}>
          <strong role="cell">{formatTime(race.scheduledStartUtc)}</strong>
          <span className="race-name" role="cell"><small>R{race.raceNumber}</small>{race.name}</span>
          <span role="cell">{meeting.racecourse.name}</span>
          <span role="cell"><span className="code-badge">{displayCode(race.code)}</span></span>
          <span role="cell">{race.runnerCount}</span>
          <span role="cell"><span className={`race-status race-status--${race.status.toLowerCase()}`}>{race.status}</span></span>
        </div>
      ))}
    </div>
  );
};

const DashboardSkeleton = () => (
  <div className="skeleton" aria-label="Loading racing data">
    <div className="skeleton__notice" />
    <div className="skeleton__overview"><div /><div /></div>
    <div className="skeleton__heading" />
    <div className="skeleton__cards"><div /><div /><div /></div>
  </div>
);

export default App;
