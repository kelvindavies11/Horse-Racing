import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import App from "./App";
import type {
  AuditSnapshot,
  CuratedEntity,
  CuratedEntityPage,
  CuratedOverview,
  RaceResultsFeed,
  RelationshipGraph,
} from "./domain/curated";

const entities: CuratedEntity[] = [
  {
    id: "11111111-1111-1111-1111-111111111111",
    sourceSystem: "BHA",
    domainObjectType: "Racecourse",
    sourceKey: "ASC",
    displayName: "Ascot",
    sourceUrl: "https://www.britishhorseracing.com/racing/racecourses/ascot/",
    rawPayloadId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    rawCollectionRunId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
    lastPromotionRunId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
    firstObservedAtUtc: "2026-10-05T09:00:00Z",
    lastObservedAtUtc: "2026-10-06T08:00:00Z",
    data: { courseCode: "ASC", courseName: "Ascot", country: "GB" },
  },
  {
    id: "22222222-2222-2222-2222-222222222222",
    sourceSystem: "BHA",
    domainObjectType: "Horse",
    sourceKey: "HORSE-7",
    displayName: "Northern Signal",
    sourceUrl: "https://www.britishhorseracing.com/racing/horses/",
    rawPayloadId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
    rawCollectionRunId: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
    lastPromotionRunId: "ffffffff-ffff-ffff-ffff-ffffffffffff",
    firstObservedAtUtc: "2026-10-05T10:00:00Z",
    lastObservedAtUtc: "2026-10-06T08:30:00Z",
    data: { horseId: "HORSE-7", horseName: "Northern Signal", trainerId: "TRAINER-2" },
  },
];

const overview: CuratedOverview = {
  generatedAtUtc: "2026-10-06T09:00:00Z",
  totalEntities: 2,
  totalEntityTypes: 2,
  firstObservedAtUtc: "2026-10-05T09:00:00Z",
  lastObservedAtUtc: "2026-10-06T08:30:00Z",
  entityTypes: [
    { type: "Horse", count: 1, lastObservedAtUtc: "2026-10-06T08:30:00Z" },
    { type: "Racecourse", count: 1, lastObservedAtUtc: "2026-10-06T08:00:00Z" },
  ],
};

const relationships: RelationshipGraph = {
  generatedAtUtc: "2026-10-06T09:00:00Z",
  nodes: [
    { id: entities[1].id, type: "Horse", displayName: "Northern Signal", lastObservedAtUtc: "2026-10-06T08:30:00Z" },
    { id: "33333333-3333-3333-3333-333333333333", type: "Trainer", displayName: "Jane Rider", lastObservedAtUtc: "2026-10-06T08:30:00Z" },
  ],
  edges: [{ sourceId: entities[1].id, targetId: "33333333-3333-3333-3333-333333333333", label: "trainer id" }],
  patterns: [{ sourceType: "Horse", targetType: "Trainer", count: 1 }],
};

const audit: AuditSnapshot = {
  generatedAtUtc: "2026-10-06T09:00:00Z",
  summary: {
    totalRawRuns: 12,
    failedRawRuns: 1,
    totalPromotionRuns: 10,
    failedPromotionRuns: 0,
    lastRawRunAtUtc: "2026-10-06T08:00:00Z",
    lastPromotionRunAtUtc: "2026-10-06T08:05:00Z",
  },
  rawRuns: [{
    id: "raw-1",
    jobName: "bha-racecourses",
    sourceName: "BHA racecourses",
    sourceUrl: "https://example.test/raw",
    collectorVersion: "1.0.0",
    startedAtUtc: "2026-10-06T08:00:00Z",
    completedAtUtc: "2026-10-06T08:00:02Z",
    outcome: "Succeeded",
    httpStatusCode: 200,
    payloadId: "payload-1",
    payloadBytes: 2048,
    mediaType: "application/json",
  }],
  promotionRuns: [{
    id: "promotion-1",
    jobName: "bha-curated-promoter",
    sourceJobName: "bha-racecourses",
    sourceName: "BHA racecourses",
    sourceUrl: "https://example.test/raw",
    promoterVersion: "1.0.0",
    rawPayloadId: "payload-1",
    rawCollectionRunId: "raw-1",
    startedAtUtc: "2026-10-06T08:05:00Z",
    completedAtUtc: "2026-10-06T08:05:01Z",
    outcome: "Succeeded",
    recordsFound: 2,
    recordsUpserted: 2,
  }],
};

const raceResults: RaceResultsFeed = {
  generatedAtUtc: "2026-10-06T09:00:00Z",
  fromDate: "2026-09-29",
  toDate: "2026-10-05",
  totalRaces: 1,
  totalRunners: 3,
  weatherEnrichedRaces: 1,
  items: [{
    id: "race-1",
    sourceKey: "2026:5706:0",
    raceName: "The EBF Slip Anchor Maiden Stakes",
    courseName: "Nottingham",
    startUtc: "2026-10-01T12:23:00Z",
    raceType: "FLAT",
    raceClass: 4,
    distance: "5f 8y",
    going: "Good, Good to Firm in places",
    prizeAmount: 11000,
    prizeCurrency: "GBP",
    abandoned: false,
    winner: "Caelum (IRE)",
    location: { latitude: 52.9548, longitude: -1.127, postcode: "NG2 4BE", locationSource: "BHA" },
    weather: {
      weatherHourUtc: "2026-10-01T12:00:00Z",
      temperatureC: 17.6,
      apparentTemperatureC: 15.2,
      relativeHumidityPercent: 58,
      precipitationMillimetres: 0,
      weatherCode: 0,
      windSpeedKilometresPerHour: 14.2,
      windDirectionDegrees: 218,
      windGustKilometresPerHour: 33.5,
      sourceUrl: "https://archive-api.open-meteo.com/v1/archive?latitude=52.9548",
    },
    runners: [
      { finishPosition: 1, horseName: "Caelum (IRE)", clothNumber: 1, draw: 3, jockeyName: "Kevin Stott", trainerName: "Kevin Ryan", ownerName: "Caelum Partners", status: "Runner", bettingRatio: "8/15", finishTime: "1m 0.97s" },
      { finishPosition: 2, horseName: "Crimson Blaze (GB)", clothNumber: 2, draw: 4, jockeyName: "William Buick", trainerName: "Richard Hughes", ownerName: "Jastar Capital", status: "Runner", bettingRatio: "10/1", distanceFromWinner: "1 length", finishTime: "1m 1.14s" },
      { finishPosition: null, horseName: "Lady Branksome (IRE)", clothNumber: 3, draw: 5, jockeyName: "Non Runner", trainerName: "Michael Bell", ownerName: "Middleham Park Racing", status: "NonRunner", nonRunnerReason: "Vets Cert (Other)" },
    ],
  }],
};

const json = (value: unknown) => Promise.resolve(new Response(JSON.stringify(value), { status: 200, headers: { "Content-Type": "application/json" } }));

beforeEach(() => {
  window.history.replaceState(null, "", "#explore");
  vi.stubGlobal("scrollTo", vi.fn());
  vi.stubGlobal("fetch", vi.fn((input: RequestInfo | URL) => {
    const url = new URL(typeof input === "string" ? input : input.toString(), "http://localhost");
    if (url.pathname.endsWith("/curated/overview")) return json(overview);
    if (url.pathname.endsWith("/curated/relationships")) return json(relationships);
    if (url.pathname.endsWith("/curated/results")) return json(raceResults);
    if (url.pathname.endsWith("/admin/audit")) return json(audit);
    if (url.pathname.endsWith("/curated/entities")) {
      const type = url.searchParams.get("type");
      const search = url.searchParams.get("search")?.toLowerCase();
      const matches = entities.filter((entity) => (!type || entity.domainObjectType === type) && (!search || entity.displayName.toLowerCase().includes(search)));
      const page: CuratedEntityPage = { generatedAtUtc: overview.generatedAtUtc, page: 1, pageSize: 60, totalCount: matches.length, totalPages: matches.length ? 1 : 0, items: matches };
      return json(page);
    }
    return Promise.resolve(new Response(null, { status: 404 }));
  }));
});

afterEach(() => vi.unstubAllGlobals());

describe("curated data workspace", () => {
  it("renders only curated API entities and collection metrics", async () => {
    render(<App />);
    expect(await screen.findByRole("heading", { name: "Observed records" })).toBeInTheDocument();
    expect(screen.getAllByText("Ascot").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Northern Signal").length).toBeGreaterThan(0);
    expect(screen.getByText("No mock or fallback records")).toBeInTheDocument();
  });

  it("filters the entity directory by curated type", async () => {
    const user = userEvent.setup();
    render(<App />);
    await screen.findByRole("heading", { name: "Observed records" });
    await user.click(screen.getByRole("button", { name: "Horse1" }));
    await waitFor(() => expect(screen.getAllByTestId("entity-card")).toHaveLength(1));
    expect(within(screen.getByTestId("entity-card")).getAllByText("Northern Signal").length).toBeGreaterThan(0);
  });

  it("opens source provenance and lineage for a curated record", async () => {
    const user = userEvent.setup();
    render(<App />);
    await screen.findByRole("heading", { name: "Observed records" });
    await user.click(screen.getAllByTestId("entity-card")[0]);
    const dialog = screen.getByRole("dialog", { name: "Ascot details" });
    expect(within(dialog).getByText("Observation & lineage")).toBeInTheDocument();
    expect(within(dialog).getByText("Raw payload")).toBeInTheDocument();
    expect(within(dialog).getByRole("link", { name: /Original source/i })).toHaveAttribute("href", entities[0].sourceUrl);
  });

  it("shows inferred patterns and the admin job audit", async () => {
    const user = userEvent.setup();
    render(<App />);
    await screen.findByRole("heading", { name: "Observed records" });
    await user.click(screen.getByRole("button", { name: /Patterns/i }));
    expect(await screen.findByRole("heading", { name: "Families that travel together" })).toBeInTheDocument();
    expect(screen.getAllByText("Jane Rider").length).toBeGreaterThan(0);

    await user.click(screen.getByRole("button", { name: /Admin audit/i }));
    expect(await screen.findByRole("heading", { name: "Latest job activity" })).toBeInTheDocument();
    expect(screen.getByRole("table", { name: "Raw collection runs" })).toBeInTheDocument();
    await user.click(screen.getByRole("tab", { name: /Curated promotion/i }));
    expect(screen.getByRole("table", { name: "Curated promotion runs" })).toBeInTheDocument();
  });

  it("shows persisted race results with course and race-time weather", async () => {
    const user = userEvent.setup();
    render(<App />);
    await screen.findByRole("heading", { name: "Observed records" });
    await user.click(screen.getByRole("button", { name: /Race results/i }));
    const detail = await screen.findByTestId("race-result-detail");
    expect(within(detail).getByRole("heading", { name: "The EBF Slip Anchor Maiden Stakes" })).toBeInTheDocument();
    expect(within(detail).getAllByText("Caelum (IRE)").length).toBeGreaterThan(0);
    expect(within(detail).getByText("17.6°")).toBeInTheDocument();
    expect(within(detail).getByText("52.9548, -1.1270")).toBeInTheDocument();
    expect(within(detail).getByRole("table", { name: /Finishing order/i })).toBeInTheDocument();
  });
});
