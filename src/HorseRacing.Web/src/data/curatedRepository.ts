import type {
  AuditSnapshot,
  CuratedEntityConnections,
  CuratedEntityPage,
  CuratedOverview,
  ImportControlSnapshot,
  RaceResultsFeed,
  RelationshipGraph,
  StartImportResponse,
} from "../domain/curated";

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "/api").replace(/\/$/, "");

export class CuratedApiError extends Error {
  constructor(message: string, readonly status?: number) {
    super(message);
    this.name = "CuratedApiError";
  }
}

const readJson = async <T>(path: string, signal?: AbortSignal): Promise<T> => {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: { Accept: "application/json" },
    signal,
  });

  if (!response.ok) {
    throw new CuratedApiError(`The curated API returned ${response.status}.`, response.status);
  }

  return (await response.json()) as T;
};

const postJson = async <T>(path: string): Promise<T> => {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "X-Import-Control": "start",
    },
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => undefined) as { detail?: string } | undefined;
    throw new CuratedApiError(problem?.detail ?? `The import API returned ${response.status}.`, response.status);
  }

  return (await response.json()) as T;
};

export const curatedRepository = {
  getOverview(signal?: AbortSignal) {
    return readJson<CuratedOverview>("/v1/curated/overview", signal);
  },

  getEntities(type: string, search: string, page: number, signal?: AbortSignal) {
    const parameters = new URLSearchParams({ page: String(page), pageSize: "60" });
    if (type) parameters.set("type", type);
    if (search.trim()) parameters.set("search", search.trim());
    return readJson<CuratedEntityPage>(`/v1/curated/entities?${parameters}`, signal);
  },

  getEntityConnections(entityId: string, signal?: AbortSignal) {
    return readJson<CuratedEntityConnections>(
      `/v1/curated/entities/${encodeURIComponent(entityId)}/connections?limit=100`,
      signal,
    );
  },

  getRelationships(signal?: AbortSignal) {
    return readJson<RelationshipGraph>("/v1/curated/relationships?limit=500", signal);
  },

  getResults(options?: { from?: string; to?: string; signal?: AbortSignal }) {
    const parameters = new URLSearchParams();
    if (options?.from) parameters.set("from", options.from);
    if (options?.to) parameters.set("to", options.to);
    const query = parameters.size > 0 ? `?${parameters}` : "";
    return readJson<RaceResultsFeed>(`/v1/curated/results${query}`, options?.signal);
  },

  getAudit(signal?: AbortSignal) {
    return readJson<AuditSnapshot>("/v1/admin/audit?limit=100", signal);
  },

  getImports(signal?: AbortSignal) {
    return readJson<ImportControlSnapshot>("/v1/admin/imports", signal);
  },

  startImport(phaseId: string) {
    return postJson<StartImportResponse>(`/v1/admin/imports/${encodeURIComponent(phaseId)}/start`);
  },
};
