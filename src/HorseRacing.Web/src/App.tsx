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
  CuratedRaceResult,
  CuratedEntity,
  JsonValue,
  PromotionRunAudit,
  RawRunAudit,
  RelationshipGraph,
  RelationshipNode,
  RunnerResultView,
} from "./domain/curated";
import { useAudit, useCuratedData, useRaceResults } from "./hooks/useCuratedData";

type AppView = "results" | "explore" | "patterns" | "admin";
type AuditTab = "raw" | "curated";

const Icon = ({ children, ...props }: SVGProps<SVGSVGElement> & { children: ReactNode }) => (
  <svg aria-hidden="true" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...props}>{children}</svg>
);

const CompassIcon = () => <Icon><circle cx="12" cy="12" r="9" /><path d="m15.5 8.5-2.2 4.8-4.8 2.2 2.2-4.8 4.8-2.2Z" /></Icon>;
const NodesIcon = () => <Icon><circle cx="5" cy="12" r="2.5" /><circle cx="18" cy="6" r="2.5" /><circle cx="18" cy="18" r="2.5" /><path d="m7.3 10.9 8.4-3.8M7.3 13.1l8.4 3.8" /></Icon>;
const ShieldIcon = () => <Icon><path d="M12 3 5 6v5c0 4.8 2.8 8.2 7 10 4.2-1.8 7-5.2 7-10V6l-7-3Z" /><path d="m9 12 2 2 4-4" /></Icon>;
const TrophyIcon = () => <Icon><path d="M8 4h8v4c0 3-1.8 5-4 5s-4-2-4-5V4Z" /><path d="M8 6H5v2c0 2 1.2 3 3.3 3M16 6h3v2c0 2-1.2 3-3.3 3M12 13v4m-4 3h8M9 17h6" /></Icon>;
const SearchIcon = () => <Icon><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 4 4" /></Icon>;
const RefreshIcon = () => <Icon><path d="M20 7v5h-5M4 17v-5h5" /><path d="M6.1 8.1A7.5 7.5 0 0 1 19.5 12M4.5 12a7.5 7.5 0 0 0 13.4 3.9" /></Icon>;
const ArrowIcon = () => <Icon><path d="M5 12h14m-5-5 5 5-5 5" /></Icon>;
const DatabaseIcon = () => <Icon><ellipse cx="12" cy="5" rx="8" ry="3" /><path d="M4 5v7c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12v7c0 1.7 3.6 3 8 3s8-1.3 8-3v-7" /></Icon>;
const CloseIcon = () => <Icon><path d="m6 6 12 12M18 6 6 18" /></Icon>;
const ExternalIcon = () => <Icon><path d="M14 4h6v6M20 4l-9 9" /><path d="M18 13v6a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h6" /></Icon>;
const ChevronIcon = ({ left = false }: { left?: boolean }) => <Icon className={left ? "chevron-left" : undefined}><path d="m9 18 6-6-6-6" /></Icon>;
const TickIcon = () => <Icon><path d="m5 12 4 4L19 6" /></Icon>;
const AlertIcon = () => <Icon><path d="M12 4 3 20h18L12 4Z" /><path d="M12 9v5m0 3h.01" /></Icon>;

const number = new Intl.NumberFormat("en-GB");
const dateTime = new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", timeZone: "Europe/London" });
const raceDay = new Intl.DateTimeFormat("en-GB", { weekday: "short", day: "2-digit", month: "short", timeZone: "Europe/London" });
const raceClock = new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit", timeZone: "Europe/London" });
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

const typePalette = ["#df4c33", "#4263eb", "#14866d", "#8a5cf6", "#c88405", "#d64a87", "#2878a5"];
const typeColour = (value: string) => typePalette[[...value].reduce((total, character) => total + character.charCodeAt(0), 0) % typePalette.length];
const typeStyle = (type: string) => ({ "--entity-colour": typeColour(type) }) as CSSProperties;

const currentView = (): AppView => {
  const value = window.location.hash.replace("#", "");
  return value === "results" || value === "patterns" || value === "admin" ? value : "explore";
};

function App() {
  const [view, setView] = useState<AppView>(currentView);
  const [type, setType] = useState("");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [selectedEntity, setSelectedEntity] = useState<CuratedEntity>();
  const deferredQuery = useDeferredValue(query);
  const curated = useCuratedData(type, deferredQuery, page);
  const audit = useAudit(view === "admin");
  const results = useRaceResults(view === "results");

  const navigate = (nextView: AppView) => {
    setView(nextView);
    window.history.replaceState(null, "", `#${nextView}`);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const refresh = () => {
    if (view === "admin") audit.reload();
    else if (view === "results") results.reload();
    else curated.reload();
  };

  return (
    <div className="app-shell">
      <Sidebar view={view} onNavigate={navigate} />
      <main className="main-content">
        <header className="topbar">
          <div className="mobile-brand"><span className="brand-mark">P</span><strong>Paddock</strong></div>
          <div className="breadcrumb"><span>British racing</span><ChevronIcon /><strong>{view === "admin" ? "Job audit" : view}</strong></div>
          <div className="topbar-actions"><span className="live-source"><i />Curated API</span><button className="icon-button" type="button" onClick={refresh} aria-label="Refresh data"><RefreshIcon /></button><span className="avatar" aria-label="Local user">KD</span></div>
        </header>

        {view === "results" && <ResultsPage {...results} />}
        {view === "explore" && <ExplorerPage {...curated} type={type} query={query} page={page} onTypeChange={(value) => { setType(value); setPage(1); }} onQueryChange={(value) => { setQuery(value); setPage(1); }} onPageChange={setPage} onSelect={setSelectedEntity} />}
        {view === "patterns" && <PatternsPage graph={curated.relationships} isLoading={curated.isLoading} error={curated.error} onRetry={curated.reload} />}
        {view === "admin" && <AdminPage {...audit} />}
      </main>
      {selectedEntity && <EntityDrawer entity={selectedEntity} onClose={() => setSelectedEntity(undefined)} />}
    </div>
  );
}

const Sidebar = ({ view, onNavigate }: { view: AppView; onNavigate: (view: AppView) => void }) => (
  <aside className="sidebar">
    <button className="brand" type="button" onClick={() => onNavigate("results")}><span className="brand-mark">P</span><span><strong>Paddock</strong><small>Data observatory</small></span></button>
    <nav className="primary-nav" aria-label="Primary navigation">
      <p className="nav-label">Workspace</p>
      <button className={view === "results" ? "nav-item active" : "nav-item"} type="button" onClick={() => onNavigate("results")}><TrophyIcon /><span>Race results</span><small>01</small></button>
      <button className={view === "explore" ? "nav-item active" : "nav-item"} type="button" onClick={() => onNavigate("explore")}><CompassIcon /><span>Explore</span><small>02</small></button>
      <button className={view === "patterns" ? "nav-item active" : "nav-item"} type="button" onClick={() => onNavigate("patterns")}><NodesIcon /><span>Patterns</span><small>03</small></button>
      <p className="nav-label nav-label--admin">Operations</p>
      <button className={view === "admin" ? "nav-item active" : "nav-item"} type="button" onClick={() => onNavigate("admin")}><ShieldIcon /><span>Admin audit</span><small>04</small></button>
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
  const [selectedId, setSelectedId] = useState<string>();
  const selected = filtered.find((race) => race.id === selectedId) ?? filtered[0];

  useEffect(() => {
    if (filtered.length > 0 && !filtered.some((race) => race.id === selectedId)) {
      setSelectedId(filtered[0].id);
    }
  }, [filtered, selectedId]);

  return <div className="page-wrap results-page">
    <section className="hero hero--results"><div><p className="kicker">Official result data / race context</p><h1>The finish,<br /><em>in context.</em></h1></div><div className="hero-copy"><p>Read the finishing order beside the course, going and hourly weather observed at the scheduled start.</p><span><i />BHA results · Open-Meteo history</span></div></section>
    {isLoading && <PageSkeleton />}
    {!isLoading && error && <ApiError message={error} onRetry={reload} />}
    {!isLoading && data && <>
      <section className="metric-strip result-metrics"><Metric value={number.format(data.totalRaces)} label="Completed races" note={`${data.fromDate} — ${data.toDate}`} index="01" /><Metric value={number.format(data.totalRunners)} label="Declared runners" note="Finishers and non-runners" index="02" /><Metric value={`${data.totalRaces === 0 ? 0 : Math.round((data.weatherEnrichedRaces / data.totalRaces) * 100)}%`} label="Weather coverage" note={`${data.weatherEnrichedRaces} race-time observations`} index="03" /></section>
      {data.items.length === 0 ? <EmptyResults /> : <section className="section-block results-section">
        <div className="section-heading results-heading"><div><p className="kicker">Latest available week</p><h2>Results ledger</h2></div><label className="search-box result-search"><SearchIcon /><span className="sr-only">Search race results</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Course, race or winner" /></label></div>
        {filtered.length === 0 ? <NoResultMatches onClear={() => setSearch("")} /> : <div className="results-layout">
          <div className="race-result-list" aria-label="Race results">
            {filtered.map((race) => <RaceResultListItem key={race.id} race={race} selected={race.id === selected?.id} onSelect={() => setSelectedId(race.id)} />)}
          </div>
          {selected && <RaceResultDetail race={selected} />}
        </div>}
      </section>}
    </>}
  </div>;
};

const RaceResultListItem = ({ race, selected, onSelect }: { race: CuratedRaceResult; selected: boolean; onSelect: () => void }) => <button type="button" className={selected ? "race-result-item selected" : "race-result-item"} onClick={onSelect}><span className="race-result-time"><strong>{raceClock.format(new Date(race.startUtc))}</strong><small>{raceDay.format(new Date(race.startUtc))}</small></span><span className="race-result-summary"><small>{race.courseName} · {race.distance}</small><strong>{race.raceName}</strong><span>{race.abandoned ? "Abandoned" : race.winner ? <><b>1</b>{race.winner}</> : "Result recorded"}</span></span><span className={race.weather ? "weather-pin ready" : "weather-pin"} title={race.weather ? "Race-time weather available" : "Weather pending"}>{race.weather ? `${Math.round(race.weather.temperatureC)}°` : "—"}</span></button>;

const RaceResultDetail = ({ race }: { race: CuratedRaceResult }) => <article className="race-result-detail" data-testid="race-result-detail">
  <header className="result-detail-head"><div><p className="kicker">{raceDay.format(new Date(race.startUtc))} · {raceClock.format(new Date(race.startUtc))} · {race.courseName}</p><h2>{race.raceName}</h2><div className="race-tags"><span>{race.raceType}</span>{race.raceClass && <span>Class {race.raceClass}</span>}<span>{race.distance}</span><span>{race.going}</span>{race.prizeAmount != null && <span>{new Intl.NumberFormat("en-GB", { style: "currency", currency: race.prizeCurrency ?? "GBP", maximumFractionDigits: 0 }).format(race.prizeAmount)}</span>}</div></div><span className="result-seal"><TrophyIcon /><small>{race.abandoned ? "Status" : "Winner"}</small><strong>{race.abandoned ? "Abandoned" : race.winner ?? "Recorded"}</strong></span></header>
  <div className="race-context-grid"><RaceWeatherCard race={race} /><div className="course-context"><p className="context-label">Course position</p>{race.location ? <><strong>{race.courseName}</strong><span>{race.location.latitude.toFixed(4)}, {race.location.longitude.toFixed(4)}</span><small>{race.location.postcode ?? "Postcode not recorded"} · {race.location.locationSource}</small></> : <><strong>{race.courseName}</strong><span>Location pending</span><small>Weather enrichment requires resolved coordinates.</small></>}</div></div>
  <section className="finishing-order"><div className="finishing-title"><div><p className="kicker">Official order</p><h3>{race.runners.length} declared runners</h3></div><span>Odds shown as returned by source</span></div><div className="runner-table" role="table" aria-label={`Finishing order for ${race.raceName}`}><div className="runner-row runner-row--head" role="row"><span>Pos</span><span>Horse</span><span>Jockey / trainer</span><span>SP</span><span>Distance / time</span></div>{race.runners.map((runner, index) => <RunnerResultRow key={`${runner.horseName}-${index}`} runner={runner} />)}</div></section>
</article>;

const RaceWeatherCard = ({ race }: { race: CuratedRaceResult }) => {
  const weather = race.weather;
  if (!weather) return <div className="weather-card weather-card--empty"><p className="context-label">Race-time weather</p><strong>Observation pending</strong><span>Run the result and weather sync to enrich this race.</span></div>;
  return <div className="weather-card"><div className="weather-now"><p className="context-label">Race-time weather</p><strong>{weather.temperatureC.toFixed(1)}°</strong><span>{weatherLabel(weather.weatherCode)}</span><small>Feels like {weather.apparentTemperatureC.toFixed(1)}°C</small></div><dl><div><dt>Rain</dt><dd>{weather.precipitationMillimetres.toFixed(1)} mm</dd></div><div><dt>Humidity</dt><dd>{weather.relativeHumidityPercent}%</dd></div><div><dt>Wind</dt><dd>{weather.windSpeedKilometresPerHour.toFixed(1)} km/h</dd></div><div><dt>Gusts</dt><dd>{weather.windGustKilometresPerHour.toFixed(1)} km/h</dd></div></dl><a href={weather.sourceUrl} target="_blank" rel="noreferrer">Open-Meteo at {formatDateTime(weather.weatherHourUtc)} <ExternalIcon /></a></div>;
};

const RunnerResultRow = ({ runner }: { runner: RunnerResultView }) => <div className={runner.finishPosition === 1 ? "runner-row winner" : runner.status === "NonRunner" ? "runner-row non-runner" : "runner-row"} role="row"><span className="finish-position">{runner.finishPosition ?? (runner.status === "NonRunner" ? "NR" : "—")}</span><span className="runner-horse"><i aria-hidden="true">{runner.clothNumber ?? "—"}</i><span><strong>{runner.horseName}</strong><small>Cloth {runner.clothNumber ?? "—"} · Draw {runner.draw ?? "—"}</small></span></span><span><strong>{runner.jockeyName ?? "Jockey not recorded"}</strong><small>{runner.trainerName ?? "Trainer not recorded"}</small></span><span>{runner.bettingRatio ?? "—"}</span><span>{runner.nonRunnerReason ?? runner.distanceFromWinner ?? (runner.finishPosition === 1 ? "Winner" : "—")}<small>{runner.finishTime ?? runner.status}</small></span></div>;

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
              <div className="distribution-grid">{overview.entityTypes.map((summary) => <button type="button" key={summary.type} className={type === summary.type ? "distribution-item selected" : "distribution-item"} style={typeStyle(summary.type)} onClick={() => onTypeChange(type === summary.type ? "" : summary.type)}><span className="distribution-name"><i />{summary.type}</span><strong>{number.format(summary.count)}</strong><span className="distribution-track"><i style={{ width: `${Math.max(8, (summary.count / overview.entityTypes[0].count) * 100)}%` }} /></span></button>)}</div>
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

const EntityCard = ({ entity, index, onSelect }: { entity: CuratedEntity; index: number; onSelect: (entity: CuratedEntity) => void }) => {
  const signals = Object.entries(entity.data).filter(([, value]) => ["string", "number", "boolean"].includes(typeof value)).slice(0, 2);
  return <button className="entity-card" type="button" style={typeStyle(entity.domainObjectType)} onClick={() => onSelect(entity)} data-testid="entity-card"><span className="entity-card-index">{String(index).padStart(3, "0")}</span><span className="entity-type"><i />{entity.domainObjectType}</span><span className="entity-arrow"><ArrowIcon /></span><strong>{entity.displayName}</strong><small className="source-key">{entity.sourceKey}</small><span className="signal-row">{signals.length > 0 ? signals.map(([key, value]) => <span key={key}><small>{titleCase(key)}</small>{String(value)}</span>) : <span><small>Source system</small>{entity.sourceSystem}</span>}</span><span className="entity-card-foot"><span>Last seen</span>{formatRelative(entity.lastObservedAtUtc)}</span></button>;
};

const EntityDrawer = ({ entity, onClose }: { entity: CuratedEntity; onClose: () => void }) => {
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => { if (event.key === "Escape") onClose(); };
    document.addEventListener("keydown", handleKeyDown);
    document.body.classList.add("drawer-open");
    return () => { document.removeEventListener("keydown", handleKeyDown); document.body.classList.remove("drawer-open"); };
  }, [onClose]);
  return <div className="drawer-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}><aside className="entity-drawer" role="dialog" aria-modal="true" aria-label={`${entity.displayName} details`} style={typeStyle(entity.domainObjectType)}><div className="drawer-head"><span className="entity-type"><i />{entity.domainObjectType}</span><button className="icon-button" type="button" onClick={onClose} aria-label="Close details"><CloseIcon /></button></div><div className="drawer-title"><p className="kicker">Curated entity</p><h2>{entity.displayName}</h2><code>{entity.sourceKey}</code></div><section className="drawer-section"><p className="drawer-label">Found data</p><div className="property-list">{Object.entries(entity.data).map(([key, value]) => <Property key={key} name={key} value={value} />)}</div></section><section className="drawer-section lineage"><p className="drawer-label">Observation & lineage</p><div className="lineage-timeline"><LineagePoint title="First observed" value={formatDateTime(entity.firstObservedAtUtc)} /><LineagePoint title="Last observed" value={formatDateTime(entity.lastObservedAtUtc)} active /></div><dl><div><dt>Raw payload</dt><dd title={entity.rawPayloadId}>{shortId(entity.rawPayloadId)}</dd></div><div><dt>Collection run</dt><dd title={entity.rawCollectionRunId}>{shortId(entity.rawCollectionRunId)}</dd></div><div><dt>Promotion run</dt><dd title={entity.lastPromotionRunId}>{shortId(entity.lastPromotionRunId)}</dd></div></dl></section><a className="source-link" href={entity.sourceUrl} target="_blank" rel="noreferrer"><span><small>Original source</small>{new URL(entity.sourceUrl).hostname}</span><ExternalIcon /></a><details className="json-details"><summary>View curated JSON</summary><pre>{JSON.stringify(entity.data, null, 2)}</pre></details></aside></div>;
};

const Property = ({ name, value }: { name: string; value: JsonValue }) => { const complex = typeof value === "object" && value !== null; return <div className={complex ? "property complex" : "property"}><span>{titleCase(name)}</span><strong>{complex ? JSON.stringify(value) : value === null ? "—" : String(value)}</strong></div>; };
const LineagePoint = ({ title, value, active = false }: { title: string; value: string; active?: boolean }) => <div className={active ? "lineage-point active" : "lineage-point"}><i /><span><small>{title}</small><strong>{value}</strong></span></div>;

const PatternsPage = ({ graph, isLoading, error, onRetry }: { graph?: RelationshipGraph; isLoading: boolean; error?: string; onRetry: () => void }) => {
  const hubs = useMemo(() => getHubs(graph), [graph]);
  const [selectedHubId, setSelectedHubId] = useState<string>();
  const selected = hubs.find((hub) => hub.node.id === selectedHubId) ?? hubs[0];
  const maxPattern = Math.max(...(graph?.patterns.map((pattern) => pattern.count) ?? [1]));
  return (
    <div className="page-wrap">
      <section className="hero hero--patterns">
        <div><p className="kicker">Relationship signals / inferred</p><h1>Follow the<br /><em>connections.</em></h1></div>
        <div className="hero-copy"><p>References inside curated records are matched to stable source keys and names, revealing the links already present in the BHA data.</p><span><i />Signals, not manufactured relationships</span></div>
      </section>
      {isLoading && <PageSkeleton />}
      {!isLoading && error && <ApiError message={error} onRetry={onRetry} />}
      {!isLoading && graph && (
        <>
          <section className="metric-strip pattern-metrics">
            <Metric value={number.format(graph.edges.length)} label="Entity links" note="In the latest 500 records" index="01" />
            <Metric value={number.format(graph.patterns.length)} label="Type patterns" note="Distinct family pairings" index="02" />
            <Metric value={number.format(hubs.length)} label="Connected entities" note="Records with at least one link" index="03" />
          </section>
          {graph.edges.length === 0 ? <NoRelationships /> : (
            <>
              <section className="section-block">
                <div className="section-heading"><div><p className="kicker">Pattern frequency</p><h2>Families that travel together</h2></div><p>Strongest inferred relationships in this sample.</p></div>
                <div className="pattern-list">
                  {graph.patterns.map((pattern, index) => (
                    <div className="pattern-row" key={`${pattern.sourceType}-${pattern.targetType}`}>
                      <span>{String(index + 1).padStart(2, "0")}</span>
                      <strong style={typeStyle(pattern.sourceType)}><i />{pattern.sourceType}</strong>
                      <ArrowIcon />
                      <strong style={typeStyle(pattern.targetType)}><i />{pattern.targetType}</strong>
                      <span className="pattern-bar"><i style={{ width: `${(pattern.count / maxPattern) * 100}%` }} /></span>
                      <b>{pattern.count}</b>
                    </div>
                  ))}
                </div>
              </section>
              <section className="section-block connection-browser">
                <div className="section-heading"><div><p className="kicker">Connection browser</p><h2>Explore the hubs</h2></div><p>Select an entity to inspect its immediate neighbourhood.</p></div>
                <div className="connection-layout">
                  <div className="hub-list">
                    {hubs.slice(0, 24).map((hub) => (
                      <button type="button" key={hub.node.id} className={selected?.node.id === hub.node.id ? "hub-item selected" : "hub-item"} onClick={() => setSelectedHubId(hub.node.id)} style={typeStyle(hub.node.type)}>
                        <i /><span><strong>{hub.node.displayName}</strong><small>{hub.node.type}</small></span><b>{hub.neighbours.length}</b>
                      </button>
                    ))}
                  </div>
                  {selected && <Neighbourhood hub={selected.node} neighbours={selected.neighbours} />}
                </div>
              </section>
            </>
          )}
        </>
      )}
    </div>
  );
};

const getHubs = (graph?: RelationshipGraph) => {
  if (!graph) return [];
  const nodes = new Map(graph.nodes.map((node) => [node.id, node]));
  return graph.nodes.map((node) => ({ node, neighbours: graph.edges.flatMap((edge) => { if (edge.sourceId === node.id && nodes.has(edge.targetId)) return [{ node: nodes.get(edge.targetId)!, label: edge.label }]; if (edge.targetId === node.id && nodes.has(edge.sourceId)) return [{ node: nodes.get(edge.sourceId)!, label: edge.label }]; return []; }) })).filter((hub) => hub.neighbours.length > 0).sort((left, right) => right.neighbours.length - left.neighbours.length);
};

const Neighbourhood = ({ hub, neighbours }: { hub: RelationshipNode; neighbours: { node: RelationshipNode; label: string }[] }) => <div className="neighbourhood"><div className="hub-focus" style={typeStyle(hub.type)}><span className="orbit orbit-one" /><span className="orbit orbit-two" /><i /><small>{hub.type}</small><strong>{hub.displayName}</strong><span>{neighbours.length} direct {neighbours.length === 1 ? "link" : "links"}</span></div><div className="neighbour-list">{neighbours.slice(0, 12).map(({ node, label }) => <div className="neighbour" key={node.id} style={typeStyle(node.type)}><span className="link-line"><i /></span><span className="neighbour-dot" /><div><small>{label} · {node.type}</small><strong>{node.displayName}</strong></div></div>)}</div></div>;

const AdminPage = ({ data, error, isLoading, reload }: { data?: AuditSnapshot; error?: string; isLoading: boolean; reload: () => void }) => {
  const [tab, setTab] = useState<AuditTab>("raw");
  const rawSuccess = data ? data.summary.totalRawRuns - data.summary.failedRawRuns : 0;
  const promotionSuccess = data ? data.summary.totalPromotionRuns - data.summary.failedPromotionRuns : 0;
  return <div className="page-wrap admin-page"><section className="hero hero--admin"><div><p className="kicker">Operations / read-only</p><h1>Pipeline,<br /><em>under oath.</em></h1></div><div className="hero-copy"><p>Every collection and promotion attempt, including failures, is visible here with its source, timing, version, payload and outcome.</p><span><i />Audit evidence is never edited here</span></div></section>{isLoading && <PageSkeleton />}{!isLoading && error && <ApiError message={error} onRetry={reload} />}{!isLoading && data && <><section className="metric-strip admin-metrics"><Metric value={number.format(data.summary.totalRawRuns)} label="Raw collections" note={`${number.format(rawSuccess)} non-failed`} index="01" /><Metric value={number.format(data.summary.totalPromotionRuns)} label="Curated promotions" note={`${number.format(promotionSuccess)} non-failed`} index="02" /><Metric value={number.format(data.summary.failedRawRuns + data.summary.failedPromotionRuns)} label="Recorded failures" note="Across both transitions" index="03" /></section><PipelineHealth data={data} /><section className="section-block audit-section"><div className="section-heading audit-heading"><div><p className="kicker">Run ledger</p><h2>Latest job activity</h2></div><div className="audit-tabs" role="tablist"><button type="button" role="tab" aria-selected={tab === "raw"} onClick={() => setTab("raw")}>Raw collection <span>{data.rawRuns.length}</span></button><button type="button" role="tab" aria-selected={tab === "curated"} onClick={() => setTab("curated")}>Curated promotion <span>{data.promotionRuns.length}</span></button></div></div>{tab === "raw" ? <RawAuditTable runs={data.rawRuns} /> : <PromotionAuditTable runs={data.promotionRuns} />}</section></>}</div>;
};

const PipelineHealth = ({ data }: { data: AuditSnapshot }) => { const hasFailures = data.summary.failedRawRuns + data.summary.failedPromotionRuns > 0; return <section className="pipeline-card"><div className="pipeline-title"><span><DatabaseIcon /></span><div><p className="kicker">Data journey</p><h2>Source to serving layer</h2></div><span className={hasFailures ? "pipeline-status warning" : "pipeline-status"}><i />{hasFailures ? "Review failures" : "No failures recorded"}</span></div><div className="pipeline-flow"><PipelineStage number="01" title="BHA sources" note="External responses" status="source" /><span className="flow-line"><i /></span><PipelineStage number="02" title="Raw layer" note={`Last run ${formatRelative(data.summary.lastRawRunAtUtc)}`} status={data.summary.failedRawRuns > 0 ? "warning" : "ok"} /><span className="flow-line"><i /></span><PipelineStage number="03" title="Curated layer" note={`Last run ${formatRelative(data.summary.lastPromotionRunAtUtc)}`} status={data.summary.failedPromotionRuns > 0 ? "warning" : "ok"} /></div></section>; };
const PipelineStage = ({ number: stage, title, note, status }: { number: string; title: string; note: string; status: string }) => <div className={`pipeline-stage ${status}`}><span>{stage}</span><i>{status === "warning" ? <AlertIcon /> : <TickIcon />}</i><strong>{title}</strong><small>{note}</small></div>;

const RawAuditTable = ({ runs }: { runs: RawRunAudit[] }) => runs.length === 0 ? <AuditEmpty layer="Raw" /> : <div className="audit-table" role="table" aria-label="Raw collection runs"><div className="audit-row audit-row--head" role="row"><span>Outcome</span><span>Job / source</span><span>Started</span><span>Duration</span><span>Response</span><span>Payload</span></div>{runs.map((run) => <div className="audit-row" role="row" key={run.id}><span><Outcome value={run.outcome} /></span><span className="job-cell"><strong>{run.jobName}</strong><small>{run.sourceName} · v{run.collectorVersion}</small>{run.errorMessage && <em>{run.errorCode}: {run.errorMessage}</em>}</span><span>{formatDateTime(run.startedAtUtc)}</span><span>{formatDuration(run.startedAtUtc, run.completedAtUtc)}</span><span>{run.httpStatusCode ?? "—"}<small>{run.mediaType ?? "No media type"}</small></span><span title={run.payloadId ?? undefined}>{formatBytes(run.payloadBytes)}<small>{shortId(run.payloadId)}</small></span></div>)}</div>;
const PromotionAuditTable = ({ runs }: { runs: PromotionRunAudit[] }) => runs.length === 0 ? <AuditEmpty layer="Curated" /> : <div className="audit-table" role="table" aria-label="Curated promotion runs"><div className="audit-row audit-row--head" role="row"><span>Outcome</span><span>Job / source</span><span>Started</span><span>Duration</span><span>Found</span><span>Upserted</span></div>{runs.map((run) => <div className="audit-row" role="row" key={run.id}><span><Outcome value={run.outcome} /></span><span className="job-cell"><strong>{run.jobName}</strong><small>{run.sourceJobName} · v{run.promoterVersion}</small>{run.errorMessage && <em>{run.errorCode}: {run.errorMessage}</em>}</span><span>{formatDateTime(run.startedAtUtc)}</span><span>{formatDuration(run.startedAtUtc, run.completedAtUtc)}</span><span>{number.format(run.recordsFound)}<small>records</small></span><span>{number.format(run.recordsUpserted)}<small>records</small></span></div>)}</div>;

const Outcome = ({ value }: { value: string }) => <span className={`outcome outcome--${value.toLowerCase()}`}><i />{value}</span>;
const AuditEmpty = ({ layer }: { layer: string }) => <div className="audit-empty"><ShieldIcon /><h3>No {layer.toLowerCase()} runs yet</h3><p>The ledger will populate after the corresponding local job has run.</p></div>;
const Pagination = ({ current, total, onChange }: { current: number; total: number; onChange: (page: number) => void }) => total <= 1 ? null : <nav className="pagination" aria-label="Entity pages"><button type="button" disabled={current <= 1} onClick={() => onChange(current - 1)}><ChevronIcon left />Previous</button><span>Page <strong>{current}</strong> of {total}</span><button type="button" disabled={current >= total} onClick={() => onChange(current + 1)}>Next<ChevronIcon /></button></nav>;

const ApiError = ({ message, onRetry }: { message: string; onRetry: () => void }) => <section className="api-error" role="alert"><span><AlertIcon /></span><div><p className="kicker">Curated API unavailable</p><h2>No substitute data is being shown.</h2><p>{message} Start the local API and confirm PostgreSQL is available.</p><code>dotnet run --project src/HorseRacing.Api</code></div><button type="button" onClick={onRetry}><RefreshIcon />Try again</button></section>;
const InlineError = ({ message }: { message: string }) => <div className="inline-error" role="alert"><AlertIcon /><span><strong>Records could not be loaded.</strong>{message}</span></div>;
const EmptyCollection = () => <section className="empty-collection"><span><DatabaseIcon /></span><p className="kicker">Curated layer is ready</p><h2>No promoted records yet.</h2><p>Collect a Raw payload, then run the promoter. This site intentionally remains empty until curated data exists.</p><div><code>dotnet run --project src/HorseRacing.Bha.RawCollector</code><code>dotnet run --project src/HorseRacing.Bha.CuratedPromoter</code></div></section>;
const NoMatches = ({ onClear }: { onClear: () => void }) => <div className="no-matches"><SearchIcon /><h3>No curated entities match</h3><p>Try a broader name, source key, or entity family.</p><button type="button" onClick={onClear}>Clear filters</button></div>;
const NoRelationships = () => <section className="empty-collection"><span><NodesIcon /></span><p className="kicker">No inferred links in this sample</p><h2>The entities are present, but their references do not meet yet.</h2><p>Links appear when a field such as horseId, fixtureId or trainerName matches another curated entity’s stable source key or display name.</p></section>;
const PageSkeleton = () => <div className="page-skeleton" aria-label="Loading curated data"><div className="skeleton-metrics"><i /><i /><i /></div><div className="skeleton-panel"><i /><i /><i /><i /></div></div>;
const EntityGridSkeleton = () => <div className="entity-grid skeleton-grid" aria-label="Loading entities">{Array.from({ length: 6 }, (_, index) => <i key={index} />)}</div>;

export default App;
