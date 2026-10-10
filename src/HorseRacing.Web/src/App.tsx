import {
  useDeferredValue,
  useEffect,
  useMemo,
  useState,
  type CSSProperties,
  type ReactNode,
  type SVGProps,
} from "react";
import type {
  AuditSnapshot,
  CuratedEntityConnections,
  CuratedRaceResult,
  CuratedEntity,
  ImportMonthJob,
  ImportPhaseStatus,
  JsonValue,
  PromotionRunAudit,
  RawRunAudit,
  RaceResultsFeed,
  RunnerResultView,
} from "./domain/curated";
import { curatedRepository } from "./data/curatedRepository";
import { useAudit, useCuratedData, useImportControl, useRaceCalendar, useRaceResults, useWinnerInsights } from "./hooks/useCuratedData";

type AppView = "results" | "calendar" | "explore" | "patterns" | "admin" | "imports";
type AuditTab = "raw" | "curated";

const Icon = ({ children, ...props }: SVGProps<SVGSVGElement> & { children: ReactNode }) => (
  <svg aria-hidden="true" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...props}>{children}</svg>
);

const CompassIcon = () => <Icon><circle cx="12" cy="12" r="9" /><path d="m15.5 8.5-2.2 4.8-4.8 2.2 2.2-4.8 4.8-2.2Z" /></Icon>;
const NodesIcon = () => <Icon><circle cx="5" cy="12" r="2.5" /><circle cx="18" cy="6" r="2.5" /><circle cx="18" cy="18" r="2.5" /><path d="m7.3 10.9 8.4-3.8M7.3 13.1l8.4 3.8" /></Icon>;
const ShieldIcon = () => <Icon><path d="M12 3 5 6v5c0 4.8 2.8 8.2 7 10 4.2-1.8 7-5.2 7-10V6l-7-3Z" /><path d="m9 12 2 2 4-4" /></Icon>;
const TrophyIcon = () => <Icon><path d="M8 4h8v4c0 3-1.8 5-4 5s-4-2-4-5V4Z" /><path d="M8 6H5v2c0 2 1.2 3 3.3 3M16 6h3v2c0 2-1.2 3-3.3 3M12 13v4m-4 3h8M9 17h6" /></Icon>;
const CalendarIcon = () => <Icon><path d="M5 4h14a2 2 0 0 1 2 2v14H3V6a2 2 0 0 1 2-2Z" /><path d="M3 9h18M8 2v4m8-4v4M7 13h3m4 0h3m-10 4h3m4 0h3" /></Icon>;
const SearchIcon = () => <Icon><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 4 4" /></Icon>;
const RefreshIcon = () => <Icon><path d="M20 7v5h-5M4 17v-5h5" /><path d="M6.1 8.1A7.5 7.5 0 0 1 19.5 12M4.5 12a7.5 7.5 0 0 0 13.4 3.9" /></Icon>;
const ArrowIcon = () => <Icon><path d="M5 12h14m-5-5 5 5-5 5" /></Icon>;
const DatabaseIcon = () => <Icon><ellipse cx="12" cy="5" rx="8" ry="3" /><path d="M4 5v7c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12v7c0 1.7 3.6 3 8 3s8-1.3 8-3v-7" /></Icon>;
const CloseIcon = () => <Icon><path d="m6 6 12 12M18 6 6 18" /></Icon>;
const ExternalIcon = () => <Icon><path d="M14 4h6v6M20 4l-9 9" /><path d="M18 13v6a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h6" /></Icon>;
const ChevronIcon = ({ left = false }: { left?: boolean }) => <Icon className={left ? "chevron-left" : undefined}><path d="m9 18 6-6-6-6" /></Icon>;
const TickIcon = () => <Icon><path d="m5 12 4 4L19 6" /></Icon>;
const AlertIcon = () => <Icon><path d="M12 4 3 20h18L12 4Z" /><path d="M12 9v5m0 3h.01" /></Icon>;
const QueueIcon = () => <Icon><path d="M5 5h14M5 12h10M5 19h7" /><circle cx="19" cy="12" r="2" /><path d="m17.5 17.5 3 3m0-3-3 3" /></Icon>;

const number = new Intl.NumberFormat("en-GB");
const dateTime = new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", timeZone: "Europe/London" });
const raceDay = new Intl.DateTimeFormat("en-GB", { weekday: "short", day: "2-digit", month: "short", timeZone: "Europe/London" });
const raceClock = new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit", timeZone: "Europe/London" });
const calendarMonth = new Intl.DateTimeFormat("en-GB", { month: "long", year: "numeric", timeZone: "UTC" });
const calendarMeetingDay = new Intl.DateTimeFormat("en-GB", { weekday: "long", day: "numeric", month: "long", year: "numeric", timeZone: "Europe/London" });
const meetingDayNumber = new Intl.DateTimeFormat("en-GB", { day: "2-digit", timeZone: "Europe/London" });
const meetingMonthShort = new Intl.DateTimeFormat("en-GB", { month: "short", timeZone: "Europe/London" });
const relativeTime = new Intl.RelativeTimeFormat("en-GB", { numeric: "auto" });

const formatDateTime = (value?: string | null) => (value ? dateTime.format(new Date(value)) : "Not recorded");
const formatRelative = (value?: string | null) => {
  if (!value) return "No observations";
  const minutes = Math.round((new Date(value).getTime() - Date.now()) / 60_000);
  if (Math.abs(minutes) < 60) return relativeTime.format(minutes, "minute");
  const hours = Math.round(minutes / 60);
  if (Math.abs(hours) < 48) return relativeTime.format(hours, "hour");
  return relativeTime.format(Math.round(hours / 24), "day");
};
const formatBytes = (value?: number | null) => {
  if (value === undefined || value === null) return "No payload";
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / 1024 / 1024).toFixed(1)} MB`;
};
const formatDuration = (started: string, completed?: string | null) => {
  if (!completed) return "In progress";
  const milliseconds = new Date(completed).getTime() - new Date(started).getTime();
  if (milliseconds < 1000) return `${milliseconds} ms`;
  if (milliseconds < 60_000) return `${(milliseconds / 1000).toFixed(1)} sec`;
  return `${(milliseconds / 60_000).toFixed(1)} min`;
};
const shortId = (value?: string | null) => (value ? `${value.slice(0, 8)}…${value.slice(-4)}` : "—");
const titleCase = (value: string) => value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/[_-]/g, " ");
const ordinal = (value: number) => {
  const remainder = value % 100;
  if (remainder >= 11 && remainder <= 13) return `${value}th`;
  return `${value}${value % 10 === 1 ? "st" : value % 10 === 2 ? "nd" : value % 10 === 3 ? "rd" : "th"}`;
};
const resultPosition = (entity: CuratedEntity) => {
  if (!entity.domainObjectType.toLowerCase().includes("result")) return undefined;
  for (const key of ["finalPosition", "resultFinishPos", "finishPosition", "position", "ptpPosition"]) {
    const value = entity.data[key];
    if (typeof value === "number" && Number.isFinite(value) && value > 0) return ordinal(value);
    if (typeof value === "string" && value.trim()) {
      const parsed = Number(value);
      return Number.isFinite(parsed) && parsed > 0 ? ordinal(parsed) : value.trim();
    }
  }
  const status = entity.data.status;
  return typeof status === "string" && /non.?runner/i.test(status) ? "NR" : undefined;
};

const typePalette = ["#df4c33", "#4263eb", "#14866d", "#8a5cf6", "#c88405", "#d64a87", "#2878a5"];
const typeColour = (value: string) => typePalette[[...value].reduce((total, character) => total + character.charCodeAt(0), 0) % typePalette.length];
const typeStyle = (type: string) => ({ "--entity-colour": typeColour(type) }) as CSSProperties;

const useClientPagination = <T,>(items: readonly T[], pageSize: number, resetKey?: unknown) => {
  const [requestedPage, setRequestedPage] = useState(1);
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
  const page = Math.min(requestedPage, totalPages);
  const pageItems = useMemo(() => items.slice((page - 1) * pageSize, page * pageSize), [items, page, pageSize]);

  useEffect(() => setRequestedPage((current) => Math.min(current, totalPages)), [totalPages]);
  useEffect(() => setRequestedPage(1), [resetKey]);

  return { page, pageItems, totalPages, setPage: setRequestedPage };
};

const useFullscreenModal = (onClose: () => void) => {
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => { if (event.key === "Escape") onClose(); };
    document.addEventListener("keydown", handleKeyDown);
    document.body.classList.add("drawer-open");
    return () => { document.removeEventListener("keydown", handleKeyDown); document.body.classList.remove("drawer-open"); };
  }, [onClose]);
};

const londonDateKey = (value: string) => {
  const parts = new Intl.DateTimeFormat("en-GB", { year: "numeric", month: "2-digit", day: "2-digit", timeZone: "Europe/London" }).formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? "";
  return `${part("year")}-${part("month")}-${part("day")}`;
};

const shiftMonth = (month: string, offset: number) => {
  const [year, monthNumber] = month.split("-").map(Number);
  const shifted = new Date(Date.UTC(year, monthNumber - 1 + offset, 1));
  return `${shifted.getUTCFullYear()}-${String(shifted.getUTCMonth() + 1).padStart(2, "0")}`;
};

const currentView = (): AppView => {
  const value = window.location.hash.replace("#", "");
  return value === "results" || value === "calendar" || value === "patterns" || value === "admin" || value === "imports" ? value : "explore";
};

function App() {
  const [view, setView] = useState<AppView>(currentView);
  const [isSidebarExpanded, setIsSidebarExpanded] = useState(false);
  const [type, setType] = useState("");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [selectedEntity, setSelectedEntity] = useState<CuratedEntity>();
  const deferredQuery = useDeferredValue(query);
  const curated = useCuratedData(type, deferredQuery, page);
  const audit = useAudit(view === "admin");
  const imports = useImportControl(view === "imports");
  const results = useRaceResults(view === "results");
  const winnerInsights = useWinnerInsights(view === "patterns");
  const calendar = useRaceCalendar(view === "calendar");

  const navigate = (nextView: AppView) => {
    setView(nextView);
    window.history.replaceState(null, "", `#${nextView}`);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const refresh = () => {
    if (view === "admin") audit.reload();
    else if (view === "imports") imports.reload();
    else if (view === "calendar") calendar.reload();
    else if (view === "patterns") winnerInsights.reload();
    else if (view === "results") results.reload();
    else curated.reload();
  };

  return (
    <div className="app-shell">
      <Sidebar
        view={view}
        isExpanded={isSidebarExpanded}
        onNavigate={navigate}
        onToggle={() => setIsSidebarExpanded((current) => !current)}
      />
      <main className="main-content">
        <header className="topbar">
          <div className="mobile-brand"><span className="brand-mark">P</span><strong>Paddock</strong></div>
          <div className="breadcrumb"><span>British racing</span><ChevronIcon /><strong>{view === "admin" ? "Job audit" : view === "imports" ? "Import control" : view}</strong></div>
          <div className="topbar-actions"><span className="live-source"><i />Curated API</span><button className="icon-button" type="button" onClick={refresh} aria-label="Refresh data"><RefreshIcon /></button><span className="avatar" aria-label="Local user">KD</span></div>
        </header>

        {view === "results" && <ResultsPage {...results} />}
        {view === "calendar" && <CalendarPage {...calendar} />}
        {view === "explore" && <ExplorerPage {...curated} type={type} query={query} page={page} onTypeChange={(value) => { setType(value); setPage(1); }} onQueryChange={(value) => { setQuery(value); setPage(1); }} onPageChange={setPage} onSelect={setSelectedEntity} />}
        {view === "patterns" && <PatternsPage {...winnerInsights} />}
        {view === "admin" && <AdminPage {...audit} />}
        {view === "imports" && <ImportControlPage {...imports} />}
      </main>
      {selectedEntity && <EntityDrawer entity={selectedEntity} onNavigate={setSelectedEntity} onClose={() => setSelectedEntity(undefined)} />}
    </div>
  );
}

const Sidebar = ({ view, isExpanded, onNavigate, onToggle }: {
  view: AppView;
  isExpanded: boolean;
  onNavigate: (view: AppView) => void;
  onToggle: () => void;
}) => (
  <aside
    id="primary-sidebar"
    className={isExpanded ? "sidebar sidebar--expanded" : "sidebar sidebar--collapsed"}
    data-testid="primary-sidebar"
  >
    <button className="brand" type="button" aria-label={isExpanded ? undefined : "Paddock home"} onClick={() => onNavigate("results")}><span className="brand-mark">P</span><span><strong>Paddock</strong><small>Data observatory</small></span></button>
    <button
      className="sidebar-toggle"
      type="button"
      aria-label={isExpanded ? "Collapse side menu" : "Expand side menu"}
      aria-controls="primary-sidebar"
      aria-expanded={isExpanded}
      onClick={onToggle}
    ><ChevronIcon left={isExpanded} /></button>
    <nav className="primary-nav" aria-label="Primary navigation">
      <p className="nav-label">Workspace</p>
      <button className={view === "results" ? "nav-item active" : "nav-item"} type="button" aria-label="Race results" title={isExpanded ? undefined : "Race results"} onClick={() => onNavigate("results")}><TrophyIcon /><span>Race results</span><small>01</small></button>
      <button className={view === "calendar" ? "nav-item active" : "nav-item"} type="button" aria-label="Calendar" title={isExpanded ? undefined : "Calendar"} onClick={() => onNavigate("calendar")}><CalendarIcon /><span>Calendar</span><small>02</small></button>
      <button className={view === "explore" ? "nav-item active" : "nav-item"} type="button" aria-label="Explore" title={isExpanded ? undefined : "Explore"} onClick={() => onNavigate("explore")}><CompassIcon /><span>Explore</span><small>03</small></button>
      <button className={view === "patterns" ? "nav-item active" : "nav-item"} type="button" aria-label="Patterns" title={isExpanded ? undefined : "Patterns"} onClick={() => onNavigate("patterns")}><NodesIcon /><span>Patterns</span><small>04</small></button>
      <p className="nav-label nav-label--admin">Operations</p>
      <button className={view === "admin" ? "nav-item active" : "nav-item"} type="button" aria-label="Admin audit" title={isExpanded ? undefined : "Admin audit"} onClick={() => onNavigate("admin")}><ShieldIcon /><span>Admin audit</span><small>05</small></button>
      <button className={view === "imports" ? "nav-item active" : "nav-item"} type="button" aria-label="Import control" title={isExpanded ? undefined : "Import control"} onClick={() => onNavigate("imports")}><QueueIcon /><span>Import control</span><small>06</small></button>
    </nav>
    <div className="sidebar-foot"><div className="layer-card"><span><DatabaseIcon /></span><div><strong>Curated layer</strong><small>Read-only · PostgreSQL</small></div></div><p>Source → Raw → Curated<br />Local pipeline / UK</p></div>
  </aside>
);

const ResultsPage = ({ data, error, isLoading, reload }: ReturnType<typeof useRaceResults>) => {
  const [search, setSearch] = useState("");
  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!data || !query) return data?.items ?? [];
    return data.items.filter((race) =>
      race.raceName.toLowerCase().includes(query)
      || race.courseName.toLowerCase().includes(query)
      || race.winner?.toLowerCase().includes(query));
  }, [data, search]);
  const resultPages = useClientPagination(filtered, 8, search);
  const [selectedId, setSelectedId] = useState<string>();
  const selected = resultPages.pageItems.find((race) => race.id === selectedId) ?? resultPages.pageItems[0];

  useEffect(() => {
    if (resultPages.pageItems.length > 0 && !resultPages.pageItems.some((race) => race.id === selectedId)) {
      setSelectedId(resultPages.pageItems[0].id);
    }
  }, [resultPages.pageItems, selectedId]);

  return <div className="page-wrap results-page">
    <section className="hero hero--results"><div><p className="kicker">Official result data / race context</p><h1>The finish,<br /><em>in context.</em></h1></div><div className="hero-copy"><p>Read the finishing order beside the course, going and hourly weather observed at the scheduled start.</p><span><i />BHA results · Open-Meteo history</span></div></section>
    {isLoading && <PageSkeleton />}
    {!isLoading && error && <ApiError message={error} onRetry={reload} />}
    {!isLoading && data && <>
      <section className="metric-strip result-metrics"><Metric value={number.format(data.totalRaces)} label="Completed races" note={`${data.fromDate} — ${data.toDate}`} index="01" /><Metric value={number.format(data.totalRunners)} label="Declared runners" note="Finishers and non-runners" index="02" /><Metric value={`${data.totalRaces === 0 ? 0 : Math.round((data.weatherEnrichedRaces / data.totalRaces) * 100)}%`} label="Weather coverage" note={`${data.weatherEnrichedRaces} race-time observations`} index="03" /></section>
      {data.items.length === 0 ? <EmptyResults /> : <section className="section-block results-section">
        <div className="section-heading results-heading"><div><p className="kicker">Latest available week</p><h2>Results ledger</h2></div><label className="search-box result-search"><SearchIcon /><span className="sr-only">Search race results</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Course, race or winner" /></label></div>
        {filtered.length === 0 ? <NoResultMatches onClear={() => setSearch("")} /> : <div className="results-layout">
          <div className="race-result-index">
            <div className="race-result-list" aria-label="Race results">
              {resultPages.pageItems.map((race) => <RaceResultListItem key={race.id} race={race} selected={race.id === selected?.id} onSelect={() => setSelectedId(race.id)} />)}
            </div>
            <Pagination current={resultPages.page} total={resultPages.totalPages} onChange={resultPages.setPage} label="Race result pages" compact />
          </div>
          {selected && <RaceResultDetail race={selected} />}
        </div>}
      </section>}
    </>}
  </div>;
};

const RaceResultListItem = ({ race, selected, onSelect }: { race: CuratedRaceResult; selected: boolean; onSelect: () => void }) => <button type="button" className={selected ? "race-result-item selected" : "race-result-item"} onClick={onSelect}><span className="race-result-time"><strong>{raceClock.format(new Date(race.startUtc))}</strong><small>{raceDay.format(new Date(race.startUtc))}</small></span><span className="race-result-summary"><small>{race.courseName} · {race.distance}</small><strong>{race.raceName}</strong><span>{race.abandoned ? "Abandoned" : race.winner ? <><b>1</b>{race.winner}</> : "Result recorded"}</span></span><span className={race.weather ? "weather-pin ready" : "weather-pin"} title={race.weather ? "Race-time weather available" : "Weather pending"}>{race.weather ? `${Math.round(race.weather.temperatureC)}°` : "—"}</span></button>;

interface CalendarMeeting {
  key: string;
  dateKey: string;
  courseName: string;
  races: CuratedRaceResult[];
}

const groupRaceMeetings = (races: CuratedRaceResult[]) => {
  const grouped = new Map<string, CalendarMeeting>();
  for (const race of races) {
    const dateKey = londonDateKey(race.startUtc);
    const key = race.meetingSourceKey || `${dateKey}:${race.courseName}`;
    const existing = grouped.get(key);
    if (existing) existing.races.push(race);
    else grouped.set(key, { key, dateKey, courseName: race.courseName, races: [race] });
  }
  return [...grouped.values()]
    .map((meeting) => ({ ...meeting, races: meeting.races.sort((left, right) => left.startUtc.localeCompare(right.startUtc)) }))
    .sort((left, right) => left.races[0].startUtc.localeCompare(right.races[0].startUtc));
};

const calendarCells = (month: string) => {
  const [year, monthNumber] = month.split("-").map(Number);
  const firstDay = new Date(Date.UTC(year, monthNumber - 1, 1));
  const leadingDays = (firstDay.getUTCDay() + 6) % 7;
  const dayCount = new Date(Date.UTC(year, monthNumber, 0)).getUTCDate();
  const cells: ({ day: number; dateKey: string } | null)[] = Array.from({ length: leadingDays }, () => null);
  for (let day = 1; day <= dayCount; day += 1) {
    cells.push({ day, dateKey: `${month}-${String(day).padStart(2, "0")}` });
  }
  return cells;
};

const CalendarPage = ({ month, setMonth, showLatest, data, error, isLoading, reload }: ReturnType<typeof useRaceCalendar>) => {
  const meetings = useMemo(() => groupRaceMeetings(data?.items ?? []), [data]);
  const [selectedMeetingKey, setSelectedMeetingKey] = useState<string>();
  const selectedMeeting = meetings.find((meeting) => meeting.key === selectedMeetingKey);
  const [selectedRaceId, setSelectedRaceId] = useState<string>();
  const selectedRace = selectedMeeting?.races.find((race) => race.id === selectedRaceId) ?? selectedMeeting?.races[0];

  useEffect(() => {
    if (selectedMeetingKey && !meetings.some((meeting) => meeting.key === selectedMeetingKey)) {
      setSelectedMeetingKey(undefined);
    }
  }, [meetings, selectedMeetingKey]);

  useEffect(() => {
    if (selectedMeeting && !selectedMeeting.races.some((race) => race.id === selectedRaceId)) {
      setSelectedRaceId(selectedMeeting.races[0].id);
    }
  }, [selectedMeeting, selectedRaceId]);

  const meetingsByDate = useMemo(() => {
    const lookup = new Map<string, CalendarMeeting[]>();
    for (const meeting of meetings) lookup.set(meeting.dateKey, [...(lookup.get(meeting.dateKey) ?? []), meeting]);
    return lookup;
  }, [meetings]);

  return <><div className="page-wrap calendar-page">
    <section className="hero hero--calendar"><div><p className="kicker">Meetings / races / official results</p><h1>The racing<br /><em>calendar.</em></h1></div><div className="hero-copy"><p>Move through imported meetings by month, open a course card, then inspect every race, runner and observed condition.</p><span><i />All times shown in Europe/London</span></div></section>
    {isLoading && <PageSkeleton />}
    {!isLoading && error && <ApiError message={error} onRetry={reload} />}
    {!isLoading && month && data && <>
      <section className="metric-strip calendar-metrics"><Metric value={number.format(meetings.length)} label="Race meetings" note={calendarMonth.format(new Date(`${month}-01T12:00:00Z`))} index="01" /><Metric value={number.format(data.totalRaces)} label="Completed races" note={`${number.format(data.totalRunners)} declared runners`} index="02" /><Metric value={`${data.totalRaces === 0 ? 0 : Math.round((data.weatherEnrichedRaces / data.totalRaces) * 100)}%`} label="Weather coverage" note={`${data.weatherEnrichedRaces} race-hour observations`} index="03" /></section>
      <section className="section-block calendar-section">
        <div className="calendar-toolbar"><div><p className="kicker">Imported programme</p><h2>{calendarMonth.format(new Date(`${month}-01T12:00:00Z`))}</h2></div><div className="calendar-controls"><button type="button" onClick={() => setMonth(shiftMonth(month, -1))} aria-label="Previous month"><ChevronIcon left /></button><label><span>Month</span><input type="month" value={month} onChange={(event) => setMonth(event.target.value)} /></label><button type="button" onClick={() => setMonth(shiftMonth(month, 1))} aria-label="Next month"><ChevronIcon /></button><button className="latest-month" type="button" onClick={showLatest}>Latest data</button></div></div>
        {data.items.length === 0 ? <NoCalendarMeetings /> : <div className="calendar-grid" role="grid" aria-label={`${calendarMonth.format(new Date(`${month}-01T12:00:00Z`))} race meetings`}>
          {['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'].map((day) => <span className="calendar-weekday" role="columnheader" key={day}>{day}</span>)}
          {calendarCells(month).map((cell, index) => cell ? <div className={meetingsByDate.has(cell.dateKey) ? "calendar-day has-meetings" : "calendar-day"} role="gridcell" key={cell.dateKey}><span className="calendar-day-number">{cell.day}</span><div className="calendar-day-meetings">{(meetingsByDate.get(cell.dateKey) ?? []).map((meeting) => <button type="button" className={selectedMeeting?.key === meeting.key ? "calendar-meeting selected" : "calendar-meeting"} aria-pressed={selectedMeeting?.key === meeting.key} key={meeting.key} onClick={() => { setSelectedRaceId(undefined); setSelectedMeetingKey(meeting.key); }}><strong>{meeting.courseName}</strong><span>{meeting.races.length} {meeting.races.length === 1 ? "race" : "races"} · first {raceClock.format(new Date(meeting.races[0].startUtc))}</span></button>)}</div></div> : <span className="calendar-day calendar-day--empty" role="gridcell" aria-hidden="true" key={`empty-${index}`} />)}
        </div>}
      </section>
    </>}
  </div>{selectedMeeting && selectedRace && <CalendarMeetingModal meeting={selectedMeeting} selectedRace={selectedRace} onSelectRace={setSelectedRaceId} onClose={() => { setSelectedMeetingKey(undefined); setSelectedRaceId(undefined); }} />}</>;
};

const CalendarMeetingModal = ({ meeting, selectedRace, onSelectRace, onClose }: { meeting: CalendarMeeting; selectedRace: CuratedRaceResult; onSelectRace: (raceId: string) => void; onClose: () => void }) => {
  useFullscreenModal(onClose);
  const meetingDate = new Date(meeting.races[0].startUtc);
  const runnerCount = meeting.races.reduce((total, race) => total + race.runners.length, 0);
  const weatherCount = meeting.races.filter((race) => race.weather).length;
  const firstPost = raceClock.format(meetingDate);
  const lastPost = raceClock.format(new Date(meeting.races.at(-1)!.startUtc));

  return <div className="drawer-backdrop" role="presentation">
    <aside className="entity-drawer entity-modal calendar-meeting-modal" role="dialog" aria-modal="true" aria-label={`${meeting.courseName} meeting details`} style={{ "--entity-colour": "#8a5cf6" } as CSSProperties} data-testid="calendar-meeting-modal">
      <header className="entity-modal-bar">
        <div className="atlas-brand"><span>Calendar / {calendarMeetingDay.format(meetingDate)}</span><strong>Meeting atlas</strong></div>
        <div className="atlas-actions"><span>Esc to return</span><button className="icon-button modal-close" type="button" onClick={onClose} aria-label="Close meeting details"><CloseIcon /></button></div>
      </header>

      <div className="entity-modal-scroll calendar-modal-scroll">
        <section className="calendar-modal-hero">
          <div className="meeting-date-orbit" aria-hidden="true">
            <span className="meeting-orbit meeting-orbit--outer" />
            <span className="meeting-orbit meeting-orbit--inner" />
            <span className="meeting-orbit-dot dot-a" />
            <span className="meeting-orbit-dot dot-b" />
            <div className="meeting-date-card"><strong>{meetingDayNumber.format(meetingDate)}</strong><span>{meetingMonthShort.format(meetingDate)}</span><small>London</small></div>
          </div>
          <div className="calendar-modal-identity">
            <span className="entity-type"><i />Race meeting</span>
            <p className="atlas-kicker">Official results / meeting room</p>
            <h2>{meeting.courseName}</h2>
            <p className="meeting-date-line">{calendarMeetingDay.format(meetingDate)}</p>
            <div className="entity-hero-signals">
              <span><small>Race window</small><strong>{firstPost} — {lastPost}</strong></span>
              <span><small>Races</small><strong>{meeting.races.length}</strong></span>
              <span><small>Declared runners</small><strong>{runnerCount}</strong></span>
              <span><small>Weather observations</small><strong>{weatherCount} / {meeting.races.length}</strong></span>
            </div>
          </div>
        </section>

        <section className="calendar-modal-content meeting-detail" data-testid="calendar-meeting-detail">
          <div className="calendar-modal-content-head"><div><p className="kicker">Meeting card</p><h3>Choose a race</h3></div>{selectedRace.location && <div className="meeting-location"><small>Course position</small><strong>{selectedRace.location.postcode ?? "Postcode not recorded"}</strong><span>{selectedRace.location.latitude.toFixed(4)}, {selectedRace.location.longitude.toFixed(4)}</span></div>}</div>
          <div className="race-tabs" role="tablist" aria-label={`${meeting.courseName} races`}>{meeting.races.map((race) => <button type="button" role="tab" aria-selected={race.id === selectedRace.id} key={race.id} onClick={() => onSelectRace(race.id)}><strong>{raceClock.format(new Date(race.startUtc))}</strong><span>{race.raceName}</span><small>{race.runners.length} runners · {race.distance}</small></button>)}</div>
          <RaceResultDetail race={selectedRace} />
        </section>
      </div>
    </aside>
  </div>;
};

const RaceResultDetail = ({ race }: { race: CuratedRaceResult }) => {
  const runnerPages = useClientPagination(race.runners, 10, race.id);
  return <article className="race-result-detail" data-testid="race-result-detail">
    <header className="result-detail-head"><div><p className="kicker">{raceDay.format(new Date(race.startUtc))} · {raceClock.format(new Date(race.startUtc))} · {race.courseName}</p><h2>{race.raceName}</h2><div className="race-tags"><span>{race.raceType}</span>{race.raceClass && <span>Class {race.raceClass}</span>}<span>{race.distance}</span><span>{race.going}</span>{race.prizeAmount != null && <span>{new Intl.NumberFormat("en-GB", { style: "currency", currency: race.prizeCurrency ?? "GBP", maximumFractionDigits: 0 }).format(race.prizeAmount)}</span>}</div></div><span className="result-seal"><TrophyIcon /><small>{race.abandoned ? "Status" : "Winner"}</small><strong>{race.abandoned ? "Abandoned" : race.winner ?? "Recorded"}</strong></span></header>
    <div className="race-context-grid"><RaceWeatherCard race={race} /><div className="course-context"><p className="context-label">Course position</p>{race.location ? <><strong>{race.courseName}</strong><span>{race.location.latitude.toFixed(4)}, {race.location.longitude.toFixed(4)}</span><small>{race.location.postcode ?? "Postcode not recorded"} · {race.location.locationSource}</small></> : <><strong>{race.courseName}</strong><span>Location pending</span><small>Weather enrichment requires resolved coordinates.</small></>}</div></div>
    <section className="finishing-order"><div className="finishing-title"><div><p className="kicker">Official order</p><h3>{race.runners.length} declared runners</h3></div><span>Odds shown as returned by source</span></div><div className="runner-table" role="table" aria-label={`Finishing order for ${race.raceName}`}><div className="runner-row runner-row--head" role="row"><span>Pos</span><span>Horse</span><span>Jockey / trainer / owner</span><span>SP</span><span>Distance / time</span></div>{runnerPages.pageItems.map((runner, index) => <RunnerResultRow key={`${runner.horseName}-${(runnerPages.page - 1) * 10 + index}`} runner={runner} />)}</div><Pagination current={runnerPages.page} total={runnerPages.totalPages} onChange={runnerPages.setPage} label="Runner pages" /></section>
  </article>;
};

const RaceWeatherCard = ({ race }: { race: CuratedRaceResult }) => {
  const weather = race.weather;
  if (!weather) return <div className="weather-card weather-card--empty"><p className="context-label">Race-time weather</p><strong>Observation pending</strong><span>Run the result and weather sync to enrich this race.</span></div>;
  return <div className="weather-card"><div className="weather-now"><p className="context-label">Race-time weather</p><strong>{weather.temperatureC.toFixed(1)}°</strong><span>{weatherLabel(weather.weatherCode)}</span><small>Feels like {weather.apparentTemperatureC.toFixed(1)}°C</small></div><dl><div><dt>Rain</dt><dd>{weather.precipitationMillimetres.toFixed(1)} mm</dd></div><div><dt>Humidity</dt><dd>{weather.relativeHumidityPercent}%</dd></div><div><dt>Wind</dt><dd>{weather.windSpeedKilometresPerHour.toFixed(1)} km/h</dd></div><div><dt>Gusts</dt><dd>{weather.windGustKilometresPerHour.toFixed(1)} km/h</dd></div></dl><a href={weather.sourceUrl} target="_blank" rel="noreferrer">Open-Meteo at {formatDateTime(weather.weatherHourUtc)} <ExternalIcon /></a></div>;
};

const RunnerResultRow = ({ runner }: { runner: RunnerResultView }) => <div className={runner.finishPosition === 1 ? "runner-row winner" : runner.status === "NonRunner" ? "runner-row non-runner" : "runner-row"} role="row"><span className="finish-position">{runner.finishPosition ?? (runner.status === "NonRunner" ? "NR" : "—")}</span><span className="runner-horse"><i aria-hidden="true">{runner.clothNumber ?? "—"}</i><span><strong>{runner.horseName}</strong><small>Cloth {runner.clothNumber ?? "—"} · Draw {runner.draw ?? "—"}</small></span></span><span className="runner-connections"><strong>{runner.jockeyName ?? "Jockey not recorded"}</strong><small>Trainer · {runner.trainerName ?? "Not recorded"}</small><small>Owner · {runner.ownerName ?? "Not recorded"}</small></span><span>{runner.bettingRatio ?? "—"}</span><span>{runner.nonRunnerReason ?? runner.distanceFromWinner ?? (runner.finishPosition === 1 ? "Winner" : "—")}<small>{runner.finishTime ?? runner.status}</small></span></div>;

const weatherLabel = (code: number) => {
  if (code === 0) return "Clear";
  if (code <= 3) return "Cloudy";
  if (code <= 48) return "Fog";
  if (code <= 57) return "Drizzle";
  if (code <= 67) return "Rain";
  if (code <= 77) return "Snow";
  if (code <= 82) return "Showers";
  return "Storms";
};

const EmptyResults = () => <section className="empty-collection"><span><TrophyIcon /></span><p className="kicker">Result layer is ready</p><h2>No completed races have been imported yet.</h2><p>Run the results synchronisation to collect BHA results, promote the records and attach race-time weather.</p><code>dotnet run --project src/HorseRacing.RaceDataSync</code></section>;
const NoCalendarMeetings = () => <section className="empty-collection calendar-empty"><span><CalendarIcon /></span><p className="kicker">No imported meetings</p><h2>This month has no completed race results.</h2><p>Choose another month or return to the latest imported data.</p></section>;
const NoResultMatches = ({ onClear }: { onClear: () => void }) => <div className="no-matches"><SearchIcon /><h3>No race results match</h3><p>Try another course, race name or winner.</p><button type="button" onClick={onClear}>Clear search</button></div>;

interface ExplorerPageProps {
  overview?: ReturnType<typeof useCuratedData>["overview"];
  entities?: ReturnType<typeof useCuratedData>["entities"];
  error?: string;
  entitiesError?: string;
  isLoading: boolean;
  entitiesLoading: boolean;
  type: string;
  query: string;
  page: number;
  onTypeChange: (value: string) => void;
  onQueryChange: (value: string) => void;
  onPageChange: (value: number) => void;
  onSelect: (entity: CuratedEntity) => void;
  reload: () => void;
}

const ExplorerPage = ({ overview, entities, error, entitiesError, isLoading, entitiesLoading, type, query, page, onTypeChange, onQueryChange, onPageChange, onSelect, reload }: ExplorerPageProps) => (
  <div className="page-wrap">
    <section className="hero"><div><p className="kicker">Curated collection / live read model</p><h1>The shape of<br /><em>British racing.</em></h1></div><div className="hero-copy"><p>Inspect the entities we have observed, trace every record to source, and move through the data by type—not by guesswork.</p><span><i />No mock or fallback records</span></div></section>
    {isLoading && <PageSkeleton />}
    {!isLoading && error && <ApiError message={error} onRetry={reload} />}
    {!isLoading && overview && (
      <>
        <section className="metric-strip" aria-label="Curated data summary"><Metric value={number.format(overview.totalEntities)} label="Curated entities" note="Current distinct records" index="01" /><Metric value={number.format(overview.totalEntityTypes)} label="Entity families" note="Mapped source types" index="02" /><Metric value={formatRelative(overview.lastObservedAtUtc)} label="Latest observation" note={formatDateTime(overview.lastObservedAtUtc)} index="03" compact /></section>
        {overview.totalEntities === 0 ? <EmptyCollection /> : (
          <>
            <section className="section-block distribution-section">
              <div className="section-heading"><div><p className="kicker">Collection profile</p><h2>What’s in the paddock</h2></div><p>Choose a family to narrow the directory.</p></div>
              <EntityTypeGrid summaries={overview.entityTypes} selectedType={type} onTypeChange={onTypeChange} />
            </section>
            <section className="section-block directory-section">
              <div className="section-heading directory-heading"><div><p className="kicker">Entity directory</p><h2>Observed records</h2></div><span className="result-count">{number.format(entities?.totalCount ?? 0)} matches</span></div>
              <div className="filter-bar"><label className="search-box"><SearchIcon /><span className="sr-only">Search curated entities</span><input value={query} onChange={(event) => onQueryChange(event.target.value)} placeholder="Search by name or source key" /></label><label className="select-box"><span>Entity family</span><select value={type} onChange={(event) => onTypeChange(event.target.value)}><option value="">All families</option>{overview.entityTypes.map((summary) => <option key={summary.type} value={summary.type}>{summary.type} ({summary.count})</option>)}</select></label>{(type || query) && <button className="clear-button" type="button" onClick={() => { onTypeChange(""); onQueryChange(""); }}>Clear filters</button>}</div>
              {entitiesLoading && <EntityGridSkeleton />}
              {!entitiesLoading && entitiesError && <InlineError message={entitiesError} />}
              {!entitiesLoading && entities && entities.items.length === 0 && <NoMatches onClear={() => { onTypeChange(""); onQueryChange(""); }} />}
              {!entitiesLoading && entities && entities.items.length > 0 && <><div className="entity-grid">{entities.items.map((entity, index) => <EntityCard key={entity.id} entity={entity} index={index + 1 + (page - 1) * entities.pageSize} onSelect={onSelect} />)}</div><Pagination current={entities.page} total={entities.totalPages} onChange={onPageChange} /></>}
            </section>
          </>
        )}
      </>
    )}
  </div>
);

const Metric = ({ value, label, note, index, compact = false }: { value: string; label: string; note: string; index: string; compact?: boolean }) => <div className="metric"><span className="metric-index">/{index}</span><strong className={compact ? "compact" : undefined}>{value}</strong><div><span>{label}</span><small>{note}</small></div></div>;

const EntityTypeGrid = ({ summaries, selectedType, onTypeChange }: { summaries: readonly { type: string; count: number }[]; selectedType: string; onTypeChange: (value: string) => void }) => {
  const pages = useClientPagination(summaries, 8);
  const largestCount = summaries[0]?.count ?? 1;
  return <><div className="distribution-grid">{pages.pageItems.map((summary) => <button type="button" key={summary.type} className={selectedType === summary.type ? "distribution-item selected" : "distribution-item"} style={typeStyle(summary.type)} onClick={() => onTypeChange(selectedType === summary.type ? "" : summary.type)}><span className="distribution-name"><i />{summary.type}</span><strong>{number.format(summary.count)}</strong><span className="distribution-track"><i style={{ width: `${Math.max(8, (summary.count / largestCount) * 100)}%` }} /></span></button>)}</div><Pagination current={pages.page} total={pages.totalPages} onChange={pages.setPage} label="Entity family pages" /></>;
};

const EntityCard = ({ entity, index, onSelect }: { entity: CuratedEntity; index: number; onSelect: (entity: CuratedEntity) => void }) => {
  const signals = Object.entries(entity.data).filter(([, value]) => ["string", "number", "boolean"].includes(typeof value)).slice(0, 2);
  return <button className="entity-card" type="button" style={typeStyle(entity.domainObjectType)} onClick={() => onSelect(entity)} data-testid="entity-card"><span className="entity-card-index">{String(index).padStart(3, "0")}</span><span className="entity-type"><i />{entity.domainObjectType}</span><span className="entity-arrow"><ArrowIcon /></span><strong>{entity.displayName}</strong><small className="source-key">{entity.sourceKey}</small><span className="signal-row">{signals.length > 0 ? signals.map(([key, value]) => <span key={key}><small>{titleCase(key)}</small>{String(value)}</span>) : <span><small>Source system</small>{entity.sourceSystem}</span>}</span><span className="entity-card-foot"><span>Last seen</span>{formatRelative(entity.lastObservedAtUtc)}</span></button>;
};

const EntityDrawer = ({ entity, onNavigate, onClose }: { entity: CuratedEntity; onNavigate: (entity: CuratedEntity) => void; onClose: () => void }) => {
  const [connections, setConnections] = useState<CuratedEntityConnections>();
  const [connectionsError, setConnectionsError] = useState<string>();
  const [connectionsLoading, setConnectionsLoading] = useState(true);
  const properties = useMemo(() => Object.entries(entity.data), [entity.data]);
  const propertyPages = useClientPagination(properties, 12, entity.id);
  const connectionLinks = connections?.links ?? [];
  const connectionPages = useClientPagination(connectionLinks, 8, entity.id);
  useFullscreenModal(onClose);

  useEffect(() => {
    const controller = new AbortController();
    setConnections(undefined);
    setConnectionsError(undefined);
    setConnectionsLoading(true);
    curatedRepository.getEntityConnections(entity.id, controller.signal)
      .then((value) => {
        setConnections(value);
        setConnectionsLoading(false);
      })
      .catch((caught: unknown) => {
        if (!controller.signal.aborted) {
          setConnectionsError(caught instanceof Error ? caught.message : "Linked records could not be loaded.");
          setConnectionsLoading(false);
        }
      });
    return () => controller.abort();
  }, [entity.id]);

  const initial = entity.displayName.trim().charAt(0).toUpperCase() || "?";
  const position = resultPosition(entity);

  return <div className="drawer-backdrop" role="presentation">
    <aside className="entity-drawer entity-modal" role="dialog" aria-modal="true" aria-label={`${entity.displayName} details`} style={typeStyle(entity.domainObjectType)} data-testid="entity-fullscreen-modal">
      <header className="entity-modal-bar">
        <div className="atlas-brand"><span>Explore / {entity.domainObjectType}</span><strong>Record atlas</strong></div>
        <div className="atlas-actions"><span>Esc to return</span><button className="icon-button modal-close" type="button" onClick={onClose} aria-label="Close details"><CloseIcon /></button></div>
      </header>

      <div className="entity-modal-scroll">
        <section className="entity-modal-hero">
          <div className="entity-constellation">
            <span className="constellation-ring ring-one" />
            <span className="constellation-ring ring-two" />
            <span className="constellation-ring ring-three" />
            <span className="constellation-node node-one" />
            <span className="constellation-node node-two" />
            <span className="constellation-node node-three" />
            <span className="constellation-node node-four" />
            <div className="entity-monogram" data-testid="entity-type-emblem"><span className="entity-monogram-initial" aria-hidden="true">{initial}</span><small>Entity type</small><strong>{titleCase(entity.domainObjectType)}</strong>{position && <b className="result-position" data-testid="result-position"><small>Finish position</small><span>{position}</span></b>}</div>
          </div>
          <div className="entity-modal-identity">
            <span className="entity-type"><i />{entity.domainObjectType}</span>
            <p className="atlas-kicker">Curated relationship atlas</p>
            <h2>{entity.displayName}</h2>
            <code>{entity.sourceKey}</code>
            <div className="entity-hero-signals">
              <span><small>Source system</small><strong>{entity.sourceSystem}</strong></span>
              <span><small>Observed fields</small><strong>{properties.length}</strong></span>
              <span><small>Linked records</small><strong>{connectionsLoading ? "…" : connections?.links.length ?? 0}</strong></span>
            </div>
          </div>
        </section>

        <div className="entity-modal-grid">
          <section className="entity-modal-panel entity-modal-links linked-records" data-testid="linked-record-panel">
            <div className="entity-panel-heading"><div><p className="drawer-label">Relationship map</p><h3>Where this record leads</h3></div>{connections && <span>{connections.links.length} found</span>}</div>
            <div className="relationship-focus" aria-label="Current record and linked records"><span><small>Currently viewing</small><strong>{entity.displayName}</strong><code>{titleCase(entity.domainObjectType)}</code></span><i><ArrowIcon /></i><span><small>Linked destinations</small><strong>{connectionsLoading ? "Mapping…" : `${connectionLinks.length} ${connectionLinks.length === 1 ? "record" : "records"}`}</strong><code>Choose a card to continue exploring</code></span></div>
            {connectionsLoading && <p className="linked-records-state">Finding related records…</p>}
            {!connectionsLoading && connectionsError && <p className="linked-records-state error">{connectionsError}</p>}
            {!connectionsLoading && connections && connections.links.length === 0 && <p className="linked-records-state">No direct curated links were found for this record.</p>}
            {!connectionsLoading && connections && connections.links.length > 0 && <><div className="linked-record-list">{connectionPages.pageItems.map((link) => <button type="button" key={link.entity.id} style={typeStyle(link.entity.domainObjectType)} onClick={() => onNavigate(link.entity)}><i /><span><small>{link.direction} · {link.label} · {link.entity.domainObjectType}</small><strong>{link.entity.displayName}</strong><code>{link.entity.sourceKey}</code></span><ArrowIcon /></button>)}</div><Pagination current={connectionPages.page} total={connectionPages.totalPages} onChange={connectionPages.setPage} label="Linked record pages" /></>}
            {connections?.hasMore && <p className="linked-records-state">Showing the first 100 linked records.</p>}
          </section>

          <section className="entity-modal-panel entity-modal-facts">
            <div className="entity-panel-heading"><div><p className="drawer-label">Found data</p><h3>What we know</h3></div><span>{properties.length} {properties.length === 1 ? "field" : "fields"}</span></div>
            <div className="property-list">{propertyPages.pageItems.map(([key, value]) => <Property key={key} name={key} value={value} />)}</div>
            <Pagination current={propertyPages.page} total={propertyPages.totalPages} onChange={propertyPages.setPage} label="Record field pages" />
          </section>

          <section className="entity-modal-panel entity-modal-lineage lineage">
            <div className="entity-panel-heading"><div><p className="drawer-label">Observation & lineage</p><h3>A record with receipts</h3></div><span>Traceable</span></div>
            <div className="lineage-timeline"><LineagePoint title="First observed" value={formatDateTime(entity.firstObservedAtUtc)} /><LineagePoint title="Last observed" value={formatDateTime(entity.lastObservedAtUtc)} active /></div>
            <dl><div><dt>Raw payload</dt><dd title={entity.rawPayloadId}>{shortId(entity.rawPayloadId)}</dd></div><div><dt>Collection run</dt><dd title={entity.rawCollectionRunId}>{shortId(entity.rawCollectionRunId)}</dd></div><div><dt>Promotion run</dt><dd title={entity.lastPromotionRunId}>{shortId(entity.lastPromotionRunId)}</dd></div></dl>
          </section>

          <section className="entity-modal-panel entity-modal-source">
            <p className="drawer-label">Source portal</p>
            <p>This curated record can always be traced back to the page that introduced it.</p>
            <a className="source-link" href={entity.sourceUrl} target="_blank" rel="noreferrer"><span><small>Original source</small>{new URL(entity.sourceUrl).hostname}</span><ExternalIcon /></a>
          </section>
        </div>

        <details className="json-details entity-modal-json"><summary><span>Developer hatch</span> View curated JSON</summary><pre>{JSON.stringify(entity.data, null, 2)}</pre></details>
      </div>
    </aside>
  </div>;
};

const Property = ({ name, value }: { name: string; value: JsonValue }) => { const complex = typeof value === "object" && value !== null; return <div className={complex ? "property complex" : "property"}><span>{titleCase(name)}</span><strong>{complex ? JSON.stringify(value) : value === null ? "—" : String(value)}</strong></div>; };
const LineagePoint = ({ title, value, active = false }: { title: string; value: string; active?: boolean }) => <div className={active ? "lineage-point active" : "lineage-point"}><i /><span><small>{title}</small><strong>{value}</strong></span></div>;

type WinnerDimension = "jockeyName" | "trainerName" | "ownerName";

interface WinnerObservation {
  race: CuratedRaceResult;
  winner: RunnerResultView;
}

interface ConnectionInsight {
  name: string;
  wins: number;
  starts: number;
  strikeRate: number;
}

const dimensionLabels: Record<WinnerDimension, string> = {
  jockeyName: "Jockey",
  trainerName: "Trainer",
  ownerName: "Owner",
};

const raceWinner = (race: CuratedRaceResult) => race.runners.find((runner) => runner.finishPosition === 1)
  ?? race.runners.find((runner) => runner.horseName === race.winner);

const topCount = (values: Array<string | null | undefined>) => {
  const counts = new Map<string, number>();
  values.filter((value): value is string => Boolean(value?.trim())).forEach((value) => counts.set(value, (counts.get(value) ?? 0) + 1));
  return [...counts].sort((left, right) => right[1] - left[1] || left[0].localeCompare(right[0]))[0]?.[0] ?? "Not recorded";
};

const connectionInsights = (data: RaceResultsFeed, observations: WinnerObservation[], dimension: WinnerDimension): ConnectionInsight[] => {
  const starts = new Map<string, number>();
  data.items.flatMap((race) => race.runners).filter((runner) => !/non.?runner/i.test(runner.status)).forEach((runner) => {
    const name = runner[dimension]?.trim();
    if (name) starts.set(name, (starts.get(name) ?? 0) + 1);
  });
  const wins = new Map<string, number>();
  observations.forEach(({ winner }) => {
    const name = winner[dimension]?.trim();
    if (name) wins.set(name, (wins.get(name) ?? 0) + 1);
  });
  return [...wins].map(([name, count]) => ({
    name,
    wins: count,
    starts: starts.get(name) ?? count,
    strikeRate: count / Math.max(starts.get(name) ?? count, 1),
  })).sort((left, right) => right.wins - left.wins || right.strikeRate - left.strikeRate || left.name.localeCompare(right.name));
};

const PatternsPage = ({ data, isLoading, error, reload }: ReturnType<typeof useWinnerInsights>) => {
  const [dimension, setDimension] = useState<WinnerDimension>("jockeyName");
  const observations = useMemo(() => (data?.items ?? []).flatMap((race) => {
    const winner = raceWinner(race);
    return winner ? [{ race, winner }] : [];
  }), [data]);
  const connections = useMemo(() => data ? connectionInsights(data, observations, dimension) : [], [data, observations, dimension]);
  const maxWins = Math.max(...connections.map((item) => item.wins), 1);
  const courses = useMemo(() => {
    const grouped = new Map<string, WinnerObservation[]>();
    observations.forEach((observation) => grouped.set(observation.race.courseName, [...(grouped.get(observation.race.courseName) ?? []), observation]));
    return [...grouped].map(([courseName, rows]) => ({
      courseName,
      races: rows.length,
      averageField: rows.reduce((total, row) => total + row.race.runners.filter((runner) => !/non.?runner/i.test(runner.status)).length, 0) / rows.length,
      leadingJockey: topCount(rows.map((row) => row.winner.jockeyName)),
      leadingTrainer: topCount(rows.map((row) => row.winner.trainerName)),
      commonGoing: topCount(rows.map((row) => row.race.going)),
    })).sort((left, right) => right.races - left.races || left.courseName.localeCompare(right.courseName));
  }, [observations]);
  const weather = useMemo(() => {
    const grouped = new Map<string, WinnerObservation[]>();
    observations.filter((row) => row.race.weather).forEach((row) => {
      const condition = weatherLabel(row.race.weather!.weatherCode);
      grouped.set(condition, [...(grouped.get(condition) ?? []), row]);
    });
    return [...grouped].map(([condition, rows]) => ({
      condition,
      races: rows.length,
      averageTemperature: rows.reduce((total, row) => total + row.race.weather!.temperatureC, 0) / rows.length,
      averageWind: rows.reduce((total, row) => total + row.race.weather!.windSpeedKilometresPerHour, 0) / rows.length,
      leadingJockey: topCount(rows.map((row) => row.winner.jockeyName)),
      commonGoing: topCount(rows.map((row) => row.race.going)),
    })).sort((left, right) => right.races - left.races || left.condition.localeCompare(right.condition));
  }, [observations]);

  return (
    <div className="page-wrap winner-patterns-page">
      <section className="hero hero--patterns">
        <div><p className="kicker">Winner signals / recent results</p><h1>What wins,<br /><em>and where.</em></h1></div>
        <div className="hero-copy"><p>Compare winning jockeys, trainers and owners, then place those results beside racecourse and observed weather conditions.</p><span><i />Observed outcomes · not causal claims</span></div>
      </section>
      {isLoading && <PageSkeleton />}
      {!isLoading && error && <ApiError message={error} onRetry={reload} />}
      {!isLoading && data && (
        <>
          <section className="metric-strip pattern-metrics" aria-label="Winner insight coverage">
            <Metric value={number.format(data.totalRaces)} label="Races analysed" note={`${data.fromDate} — ${data.toDate}`} index="01" />
            <Metric value={number.format(observations.length)} label="Winners linked" note="Matched to declared runners" index="02" />
            <Metric value={`${data.totalRaces === 0 ? 0 : Math.round((data.weatherEnrichedRaces / data.totalRaces) * 100)}%`} label="Weather coverage" note={`${data.weatherEnrichedRaces} race-hour observations`} index="03" />
          </section>
          {observations.length === 0 ? <NoWinnerInsights /> : (
            <>
              <section className="section-block winner-connections">
                <div className="section-heading winner-insight-heading"><div><p className="kicker">Winning connections</p><h2>Winner connections</h2></div><p>Wins and strike rate within the loaded results window.</p></div>
                <div className="winner-dimension-tabs" role="group" aria-label="Winner connection type">
                  {(Object.keys(dimensionLabels) as WinnerDimension[]).map((key) => <button type="button" key={key} aria-pressed={dimension === key} onClick={() => setDimension(key)}>{dimensionLabels[key]}</button>)}
                </div>
                <div className="winner-leaderboard" role="table" aria-label={`${dimensionLabels[dimension]} winner insights`}>
                  <div className="winner-row winner-row--head" role="row"><span>Rank</span><span>{dimensionLabels[dimension]}</span><span>Wins</span><span>Starts</span><span>Strike rate</span></div>
                  {connections.slice(0, 12).map((item, index) => <div className="winner-row" role="row" key={item.name}>
                    <span>{String(index + 1).padStart(2, "0")}</span><strong>{item.name}</strong><b>{item.wins}</b><span>{item.starts}</span><span className="winner-rate"><i style={{ width: `${(item.wins / maxWins) * 100}%` }} /><b>{Math.round(item.strikeRate * 100)}%</b></span>
                  </div>)}
                </div>
              </section>
              <section className="section-block">
                <div className="section-heading"><div><p className="kicker">Venue context</p><h2>Racecourse profiles</h2></div><p>Who won, field size and the most common going at each course.</p></div>
                <div className="winner-context-grid">
                  {courses.slice(0, 8).map((course) => <article className="winner-context-card" key={course.courseName}>
                    <span>{course.races} {course.races === 1 ? "race" : "races"}</span><h3>{course.courseName}</h3>
                    <dl><div><dt>Leading jockey</dt><dd>{course.leadingJockey}</dd></div><div><dt>Leading trainer</dt><dd>{course.leadingTrainer}</dd></div><div><dt>Average field</dt><dd>{course.averageField.toFixed(1)}</dd></div><div><dt>Common going</dt><dd>{course.commonGoing}</dd></div></dl>
                  </article>)}
                </div>
              </section>
              <section className="section-block weather-insights">
                <div className="section-heading"><div><p className="kicker">Race-hour context</p><h2>Weather around winners</h2></div><p>Observed conditions at scheduled start time, grouped without implying causation.</p></div>
                <div className="weather-insight-table" role="table" aria-label="Winner weather insights">
                  <div className="weather-insight-row weather-insight-row--head" role="row"><span>Condition</span><span>Races</span><span>Average temp.</span><span>Average wind</span><span>Leading jockey</span><span>Common going</span></div>
                  {weather.map((item) => <div className="weather-insight-row" role="row" key={item.condition}><strong>{item.condition}</strong><span>{item.races}</span><span>{item.averageTemperature.toFixed(1)}°C</span><span>{item.averageWind.toFixed(1)} km/h</span><span>{item.leadingJockey}</span><span>{item.commonGoing}</span></div>)}
                </div>
              </section>
            </>
          )}
        </>
      )}
    </div>
  );
};

const ImportControlPage = ({
  data,
  error,
  isLoading,
  startingPhaseId,
  actionMessage,
  startPhase,
  reload,
}: ReturnType<typeof useImportControl>) => {
  const [phaseFilter, setPhaseFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [armedPhaseId, setArmedPhaseId] = useState<string>();
  const filteredJobs = useMemo(() => (data?.jobs ?? []).filter((job) =>
    (phaseFilter === "all" || job.phaseId === phaseFilter)
    && (statusFilter === "all" || job.status === statusFilter)), [data?.jobs, phaseFilter, statusFilter]);
  const queuePages = useClientPagination(filteredJobs, 12, `${phaseFilter}:${statusFilter}`);
  const activePhase = data?.phases.find((phase) => phase.id === data.activePhaseId);
  const runnerMessage = data?.runnerMessage && data.nextRetryAtUtc
    ? `${data.runnerMessage} Automatic retry scheduled for ${formatDateTime(data.nextRetryAtUtc)}.`
    : data?.runnerMessage;

  const requestStart = async (phase: ImportPhaseStatus) => {
    if (armedPhaseId !== phase.id) {
      setArmedPhaseId(phase.id);
      return;
    }

    const started = await startPhase(phase.id);
    if (started) setArmedPhaseId(undefined);
  };

  return <div className="page-wrap imports-page">
    <section className="hero hero--imports">
      <div><p className="kicker">Operations / guarded controls</p><h1>The queue,<br /><em>in plain sight.</em></h1></div>
      <div className="hero-copy"><p>Watch every monthly runner move from queued to complete, then resume an eligible phase without opening a terminal.</p><span><i />One guarded phase · concentrated Raw runners · one Curated writer</span></div>
    </section>
    {isLoading && <PageSkeleton />}
    {!isLoading && error && <ApiError message={error} onRetry={reload} />}
    {!isLoading && data && <>
      <section className={`runner-console runner-console--${data.runnerState.toLowerCase()}`} aria-label="Current import runner">
        <div className="runner-pulse"><span /><i /><b /></div>
        <div className="runner-copy">
          <p className="kicker">Protected runner pool</p>
          <h2>{data.isImportRunning ? activePhase?.name ?? "Import detected" : "Ready for dispatch"}</h2>
          <p>{data.isImportRunning
            ? runnerMessage
              ? runnerMessage
              : data.activeMonths.length > 0
              ? `${data.activeMonths.map((month) => calendarMonth.format(new Date(`${month}-01T00:00:00Z`))).join(", ")} ${data.activeMonths.length === 1 ? "is" : "are"} being collected behind one shared request gate.`
              : "The resume process is waiting for its next safe attempt."
            : "No BHA import process is active. An eligible phase can claim the worker pool."}</p>
        </div>
        <div className="runner-facts">
          <span><small>State</small><strong>{data.runnerState}</strong></span>
          <span><small>Next retry</small><strong>{data.nextRetryAtUtc ? formatDateTime(data.nextRetryAtUtc) : "—"}</strong></span>
          <span><small>Started</small><strong>{data.activeStartedAtUtc ? formatDateTime(data.activeStartedAtUtc) : "—"}</strong></span>
          <span><small>Raw workers</small><strong>{number.format(data.activeWorkerCount)} / {number.format(data.maximumWorkerCount)}</strong></span>
          <span><small>Protected processes</small><strong>{number.format(data.activeProcessCount)}</strong></span>
        </div>
      </section>

      <section className="metric-strip import-metrics">
        <Metric value={number.format(data.totalMonths)} label="Monthly runners" note="Across the active import plan" index="01" />
        <Metric value={number.format(data.succeededMonths)} label="Succeeded" note={`${Math.round((data.succeededMonths / data.totalMonths) * 100)}% of the archive`} index="02" />
        <Metric value={number.format(data.failedMonths)} label="Need retry" note="Safe to resume by phase" index="03" />
        <Metric value={number.format(data.queuedMonths)} label="Still queued" note="Future and untouched months" index="04" />
      </section>

      {actionMessage && <div className="import-action-message" role="status"><QueueIcon /><span>{actionMessage}</span></div>}

      <section className="section-block phase-section">
        <div className="section-heading"><div><p className="kicker">Fixed dispatch plan</p><h2>Import phases</h2></div><p>Controls call only the repository’s reviewed resume scripts. A second confirmation is required before dispatch.</p></div>
        <div className="phase-grid">
          {data.phases.map((phase, index) => <ImportPhaseCard
            key={phase.id}
            phase={phase}
            index={index + 1}
            armed={armedPhaseId === phase.id}
            starting={startingPhaseId === phase.id}
            onStart={() => void requestStart(phase)}
            onCancel={() => setArmedPhaseId(undefined)}
          />)}
        </div>
      </section>

      <section className="section-block import-queue-section">
        <div className="section-heading import-queue-heading">
          <div><p className="kicker">{number.format(data.totalMonths)} monthly runners</p><h2>Dispatch queue</h2></div>
          <div className="queue-filters">
            <label><span>Phase</span><select value={phaseFilter} onChange={(event) => setPhaseFilter(event.target.value)}><option value="all">All phases</option>{data.phases.map((phase) => <option key={phase.id} value={phase.id}>{phase.name}</option>)}</select></label>
            <label><span>Status</span><select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}><option value="all">All states</option><option value="Waiting">Waiting</option><option value="Running">Running</option><option value="Succeeded">Succeeded</option><option value="Failed">Need retry</option><option value="Queued">Queued</option></select></label>
          </div>
        </div>
        <div className="import-table" role="table" aria-label="Monthly import queue">
          <div className="import-row import-row--head" role="row"><span>Runner</span><span>Phase</span><span>Status</span><span>Raw / download</span><span>Curated audit</span><span>Attempts</span><span>Started</span><span>Finished</span><span>Exit</span></div>
          {queuePages.pageItems.map((job) => <div className={`import-row import-row--${job.status.toLowerCase().replace(" ", "-")}`} role="row" key={job.id}>
            <span><strong>{calendarMonth.format(new Date(`${job.month}-01T00:00:00Z`))}</strong><small>{job.from} → {job.to}</small></span>
            <span>{job.phaseName}</span>
            <span><Outcome value={job.status} /></span>
            <ImportAuditCell label="Raw" counts={job.auditCounts.raw} requestProgress={job.requestProgress} />
            <ImportAuditCell label="Curated" counts={job.auditCounts.curated} showSkipped />
            <span>{number.format(job.attempts)}</span>
            <span>{job.startedAtUtc ? formatDateTime(job.startedAtUtc) : "—"}</span>
            <span>{job.completedAtUtc ? formatDateTime(job.completedAtUtc) : "—"}</span>
            <span>{job.exitCode ?? "—"}</span>
          </div>)}
          {filteredJobs.length === 0 && <div className="queue-empty"><QueueIcon /><strong>No runners match these filters.</strong></div>}
        </div>
        <Pagination current={queuePages.page} total={queuePages.totalPages} onChange={queuePages.setPage} label="Import queue pages" />
      </section>
    </>}
  </div>;
};

const ImportPhaseCard = ({
  phase,
  index,
  armed,
  starting,
  onStart,
  onCancel,
}: {
  phase: ImportPhaseStatus;
  index: number;
  armed: boolean;
  starting: boolean;
  onStart: () => void;
  onCancel: () => void;
}) => {
  const progress = phase.totalMonths === 0 ? 0 : Math.round((phase.succeededMonths / phase.totalMonths) * 100);
  const progressStyle = { "--phase-progress": `${progress}%` } as CSSProperties;
  return <article className={`phase-card phase-card--${phase.status.toLowerCase().replace(" ", "-")}`} style={progressStyle}>
    <div className="phase-card-top"><span className="phase-index">0{index}</span><Outcome value={phase.status} /></div>
    <h3>{phase.name}</h3>
    <p>{phase.description}</p>
    <div className="phase-range"><span>{phase.startMonth}</span><i /><span>{phase.endMonth}</span></div>
    <div className="phase-progress"><span><i /></span><small>{phase.succeededMonths} / {phase.totalMonths} succeeded · {progress}%</small></div>
    <div className="phase-counts"><span><strong>{phase.failedMonths}</strong> retry</span><span><strong>{phase.queuedMonths}</strong> queued</span><span><strong>{phase.runningMonths}</strong> running</span></div>
    {armed ? <div className="phase-confirm" role="group" aria-label={`Confirm ${phase.name}`}><p>This will claim the protected runner pool and refresh the public BHA token.</p><button type="button" onClick={onStart} disabled={starting}>{starting ? "Starting…" : "Confirm dispatch"}</button><button type="button" onClick={onCancel}>Cancel</button></div> : <button className="phase-start" type="button" onClick={onStart} disabled={!phase.canStart || starting}>{starting ? "Starting…" : phase.failedMonths > 0 ? "Resume & retry" : "Start phase"}<ArrowIcon /></button>}
    {!armed && phase.startBlocker && <small className="phase-blocker">{phase.startBlocker}</small>}
  </article>;
};

const ImportAuditCell = ({
  label,
  counts,
  showSkipped = false,
  requestProgress,
}: {
  label: string;
  counts: ImportMonthJob["auditCounts"]["raw"];
  showSkipped?: boolean;
  requestProgress?: ImportMonthJob["requestProgress"];
}) => <span className="import-audit-cell" aria-label={`${label} audit: ${counts.total} total, ${counts.succeeded} succeeded, ${counts.failed} failed, ${counts.running} running, ${counts.cancelled} cancelled${showSkipped ? `, ${counts.skipped} skipped` : ""}`}>
  <span className="import-audit-total"><strong>{number.format(counts.total)}</strong><small>records</small></span>
  <span className="import-audit-counts">
    <i className="import-audit-count import-audit-count--succeeded" title="Succeeded">OK {number.format(counts.succeeded)}</i>
    <i className="import-audit-count import-audit-count--failed" title="Failed">Fail {number.format(counts.failed)}</i>
    <i className="import-audit-count import-audit-count--running" title="Running">Run {number.format(counts.running)}</i>
    {showSkipped && <i className="import-audit-count import-audit-count--skipped" title="Skipped">Skip {number.format(counts.skipped)}</i>}
    <i className="import-audit-count import-audit-count--cancelled" title="Cancelled">Stop {number.format(counts.cancelled)}</i>
  </span>
  {requestProgress && requestProgress.estimatedRequests > 0 && <span
    className="import-request-progress"
    aria-label={`BHA request coverage: ${requestProgress.coveredRequests} of approximately ${requestProgress.estimatedRequests} source responses, ${requestProgress.percent} percent`}
  >
    <span><strong>{number.format(requestProgress.coveredRequests)}</strong> / ~{number.format(requestProgress.estimatedRequests)} sources</span>
    <b><i style={{ "--request-progress": `${requestProgress.percent}%` } as CSSProperties} /></b>
    <small>{requestProgress.percent}% downloaded</small>
  </span>}
</span>;

const AdminPage = ({ data, error, isLoading, reload }: { data?: AuditSnapshot; error?: string; isLoading: boolean; reload: () => void }) => {
  const [tab, setTab] = useState<AuditTab>("raw");
  const runningJobs = data
    ? data.summary.runningRawRuns + data.summary.runningPromotionRuns
    : 0;
  return <div className="page-wrap admin-page"><section className="hero hero--admin"><div><p className="kicker">Operations / read-only</p><h1>Pipeline,<br /><em>under oath.</em></h1></div><div className="hero-copy"><p>Every collection and promotion attempt, including failures, is visible here with its source, timing, version, payload and outcome.</p><span><i />Audit evidence refreshes every five seconds</span></div></section>{isLoading && <PageSkeleton />}{!isLoading && error && <ApiError message={error} onRetry={reload} />}{!isLoading && data && <><section className="metric-strip admin-metrics"><Metric value={number.format(runningJobs)} label="Jobs running" note={`${number.format(data.summary.runningRawRuns)} Raw · ${number.format(data.summary.runningPromotionRuns)} Curated`} index="01" /><Metric value={number.format(data.summary.totalRawRuns)} label="Raw collections" note={`${number.format(data.summary.failedRawRuns)} failed`} index="02" /><Metric value={number.format(data.summary.totalPromotionRuns)} label="Curated promotions" note={`${number.format(data.summary.failedPromotionRuns)} failed`} index="03" /><Metric value={number.format(data.summary.failedRawRuns + data.summary.failedPromotionRuns)} label="Recorded failures" note="Across both transitions" index="04" /></section><PipelineHealth data={data} /><section className="section-block audit-section"><div className="section-heading audit-heading"><div><p className="kicker">Run ledger</p><h2>Latest job activity</h2></div><div className="audit-tabs" role="tablist"><button type="button" role="tab" aria-selected={tab === "raw"} onClick={() => setTab("raw")}>Raw collection <span>{data.rawRuns.length}</span></button><button type="button" role="tab" aria-selected={tab === "curated"} onClick={() => setTab("curated")}>Curated promotion <span>{data.promotionRuns.length}</span></button></div></div>{tab === "raw" ? <RawAuditTable runs={data.rawRuns} /> : <PromotionAuditTable runs={data.promotionRuns} />}</section></>}</div>;
};

const PipelineHealth = ({ data }: { data: AuditSnapshot }) => { const hasFailures = data.summary.failedRawRuns + data.summary.failedPromotionRuns > 0; const runningJobs = data.summary.runningRawRuns + data.summary.runningPromotionRuns; return <section className="pipeline-card"><div className="pipeline-title"><span><DatabaseIcon /></span><div><p className="kicker">Data journey</p><h2>Source to serving layer</h2></div><span className={hasFailures ? "pipeline-status warning" : runningJobs > 0 ? "pipeline-status running" : "pipeline-status"}><i />{runningJobs > 0 ? `${runningJobs} ${runningJobs === 1 ? "job" : "jobs"} in progress` : hasFailures ? "Review failures" : "No failures recorded"}</span></div><div className="pipeline-flow"><PipelineStage number="01" title="BHA sources" note="External responses" status="source" /><span className="flow-line"><i /></span><PipelineStage number="02" title="Raw layer" note={`Last run ${formatRelative(data.summary.lastRawRunAtUtc)}`} status={data.summary.runningRawRuns > 0 ? "running" : data.summary.failedRawRuns > 0 ? "warning" : "ok"} /><span className="flow-line"><i /></span><PipelineStage number="03" title="Curated layer" note={`Last run ${formatRelative(data.summary.lastPromotionRunAtUtc)}`} status={data.summary.runningPromotionRuns > 0 ? "running" : data.summary.failedPromotionRuns > 0 ? "warning" : "ok"} /></div></section>; };
const PipelineStage = ({ number: stage, title, note, status }: { number: string; title: string; note: string; status: string }) => <div className={`pipeline-stage ${status}`}><span>{stage}</span><i>{status === "warning" ? <AlertIcon /> : <TickIcon />}</i><strong>{title}</strong><small>{note}</small></div>;

const RawAuditTable = ({ runs }: { runs: RawRunAudit[] }) => {
  const pages = useClientPagination(runs, 10);
  if (runs.length === 0) return <AuditEmpty layer="Raw" />;
  return <><div className="audit-table" role="table" aria-label="Raw collection runs"><div className="audit-row audit-row--head" role="row"><span>Status</span><span>Job / source</span><span>Started</span><span>Finished</span><span>Duration</span><span>Response</span><span>Payload</span></div>{pages.pageItems.map((run) => <div className="audit-row" role="row" key={run.id}><span><Outcome value={run.outcome} /></span><span className="job-cell"><strong>{run.jobName}</strong><small>{run.sourceName} · v{run.collectorVersion}</small>{run.errorMessage && <em>{run.errorCode}: {run.errorMessage}</em>}</span><span>{formatDateTime(run.startedAtUtc)}</span><span>{run.completedAtUtc ? formatDateTime(run.completedAtUtc) : <strong className="in-progress">In progress</strong>}</span><span>{formatDuration(run.startedAtUtc, run.completedAtUtc)}</span><span>{run.httpStatusCode ?? "—"}<small>{run.mediaType ?? "No media type"}</small></span><span title={run.payloadId ?? undefined}>{formatBytes(run.payloadBytes)}<small>{shortId(run.payloadId)}</small></span></div>)}</div><Pagination current={pages.page} total={pages.totalPages} onChange={pages.setPage} label="Raw audit pages" /></>;
};
const PromotionAuditTable = ({ runs }: { runs: PromotionRunAudit[] }) => {
  const pages = useClientPagination(runs, 10);
  if (runs.length === 0) return <AuditEmpty layer="Curated" />;
  return <><div className="audit-table" role="table" aria-label="Curated promotion runs"><div className="audit-row audit-row--head" role="row"><span>Status</span><span>Job / source</span><span>Started</span><span>Finished</span><span>Duration</span><span>Found</span><span>Upserted</span></div>{pages.pageItems.map((run) => <div className="audit-row" role="row" key={run.id}><span><Outcome value={run.outcome} /></span><span className="job-cell"><strong>{run.jobName}</strong><small>{run.sourceJobName} · v{run.promoterVersion}</small>{run.errorMessage && <em>{run.errorCode}: {run.errorMessage}</em>}</span><span>{formatDateTime(run.startedAtUtc)}</span><span>{run.completedAtUtc ? formatDateTime(run.completedAtUtc) : <strong className="in-progress">In progress</strong>}</span><span>{formatDuration(run.startedAtUtc, run.completedAtUtc)}</span><span>{number.format(run.recordsFound)}<small>records</small></span><span>{number.format(run.recordsUpserted)}<small>records</small></span></div>)}</div><Pagination current={pages.page} total={pages.totalPages} onChange={pages.setPage} label="Curated audit pages" /></>;
};

const Outcome = ({ value }: { value: string }) => <span className={`outcome outcome--${value.toLowerCase().replace(/\s+/g, "-")}`}><i />{value === "Running" ? "In progress" : value}</span>;
const AuditEmpty = ({ layer }: { layer: string }) => <div className="audit-empty"><ShieldIcon /><h3>No {layer.toLowerCase()} runs yet</h3><p>The ledger will populate after the corresponding local job has run.</p></div>;
const Pagination = ({ current, total, onChange, label = "Data pages", compact = false }: { current: number; total: number; onChange: (page: number) => void; label?: string; compact?: boolean }) => total <= 1 ? null : <nav className={compact ? "pagination pagination--compact" : "pagination"} aria-label={label}><button type="button" disabled={current <= 1} onClick={() => onChange(current - 1)}><ChevronIcon left /><span>Previous</span></button><span>Page <strong>{current}</strong> of {total}</span><button type="button" disabled={current >= total} onClick={() => onChange(current + 1)}><span>Next</span><ChevronIcon /></button></nav>;

const ApiError = ({ message, onRetry }: { message: string; onRetry: () => void }) => <section className="api-error" role="alert"><span><AlertIcon /></span><div><p className="kicker">Curated API unavailable</p><h2>No substitute data is being shown.</h2><p>{message} Start the local API and confirm PostgreSQL is available.</p><code>dotnet run --project src/HorseRacing.Api</code></div><button type="button" onClick={onRetry}><RefreshIcon />Try again</button></section>;
const InlineError = ({ message }: { message: string }) => <div className="inline-error" role="alert"><AlertIcon /><span><strong>Records could not be loaded.</strong>{message}</span></div>;
const EmptyCollection = () => <section className="empty-collection"><span><DatabaseIcon /></span><p className="kicker">Curated layer is ready</p><h2>No promoted records yet.</h2><p>Collect a Raw payload, then run the promoter. This site intentionally remains empty until curated data exists.</p><div><code>dotnet run --project src/HorseRacing.Bha.RawCollector</code><code>dotnet run --project src/HorseRacing.Bha.CuratedPromoter</code></div></section>;
const NoMatches = ({ onClear }: { onClear: () => void }) => <div className="no-matches"><SearchIcon /><h3>No curated entities match</h3><p>Try a broader name, source key, or entity family.</p><button type="button" onClick={onClear}>Clear filters</button></div>;
const NoWinnerInsights = () => <section className="empty-collection"><span><TrophyIcon /></span><p className="kicker">No completed winners in this window</p><h2>Winner insights need a declared result.</h2><p>The page will populate when the curated results include a runner matched to first place or the recorded winner name.</p></section>;
const PageSkeleton = () => <div className="page-skeleton" aria-label="Loading curated data"><div className="skeleton-metrics"><i /><i /><i /></div><div className="skeleton-panel"><i /><i /><i /><i /></div></div>;
const EntityGridSkeleton = () => <div className="entity-grid skeleton-grid" aria-label="Loading entities">{Array.from({ length: 6 }, (_, index) => <i key={index} />)}</div>;

export default App;
