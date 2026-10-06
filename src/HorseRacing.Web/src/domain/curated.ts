export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };

export interface EntityTypeSummary {
  type: string;
  count: number;
  lastObservedAtUtc: string;
}

export interface CuratedOverview {
  generatedAtUtc: string;
  totalEntities: number;
  totalEntityTypes: number;
  firstObservedAtUtc?: string | null;
  lastObservedAtUtc?: string | null;
  entityTypes: EntityTypeSummary[];
}

export interface CuratedEntity {
  id: string;
  sourceSystem: string;
  domainObjectType: string;
  sourceKey: string;
  displayName: string;
  sourceUrl: string;
  rawPayloadId: string;
  rawCollectionRunId: string;
  lastPromotionRunId: string;
  firstObservedAtUtc: string;
  lastObservedAtUtc: string;
  data: Record<string, JsonValue>;
}

export interface CuratedEntityPage {
  generatedAtUtc: string;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: CuratedEntity[];
}

export interface RelationshipNode {
  id: string;
  type: string;
  displayName: string;
  lastObservedAtUtc: string;
}

export interface RelationshipEdge {
  sourceId: string;
  targetId: string;
  label: string;
}

export interface RelationshipPattern {
  sourceType: string;
  targetType: string;
  count: number;
}

export interface RelationshipGraph {
  generatedAtUtc: string;
  nodes: RelationshipNode[];
  edges: RelationshipEdge[];
  patterns: RelationshipPattern[];
}

export interface AuditSummary {
  totalRawRuns: number;
  failedRawRuns: number;
  totalPromotionRuns: number;
  failedPromotionRuns: number;
  lastRawRunAtUtc?: string | null;
  lastPromotionRunAtUtc?: string | null;
}

export interface RawRunAudit {
  id: string;
  jobName: string;
  sourceName: string;
  sourceUrl: string;
  collectorVersion: string;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  outcome: string;
  httpStatusCode?: number | null;
  payloadId?: string | null;
  payloadBytes?: number | null;
  mediaType?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
}

export interface PromotionRunAudit {
  id: string;
  jobName: string;
  sourceJobName: string;
  sourceName: string;
  sourceUrl: string;
  promoterVersion: string;
  rawPayloadId: string;
  rawCollectionRunId: string;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  outcome: string;
  recordsFound: number;
  recordsUpserted: number;
  errorCode?: string | null;
  errorMessage?: string | null;
}

export interface AuditSnapshot {
  generatedAtUtc: string;
  summary: AuditSummary;
  rawRuns: RawRunAudit[];
  promotionRuns: PromotionRunAudit[];
}
