import { useCallback, useEffect, useState } from "react";
import { curatedRepository } from "../data/curatedRepository";
import type {
  AuditSnapshot,
  CuratedEntityPage,
  CuratedOverview,
  RaceResultsFeed,
  RelationshipGraph,
} from "../domain/curated";

interface FoundationState {
  overview?: CuratedOverview;
  relationships?: RelationshipGraph;
  error?: string;
  isLoading: boolean;
}

const messageFor = (error: unknown) =>
  error instanceof Error ? error.message : "The curated data could not be loaded.";

export const useCuratedData = (type: string, search: string, page: number) => {
  const [reloadKey, setReloadKey] = useState(0);
  const [foundation, setFoundation] = useState<FoundationState>({ isLoading: true });
  const [entities, setEntities] = useState<CuratedEntityPage>();
  const [entitiesError, setEntitiesError] = useState<string>();
  const [entitiesLoading, setEntitiesLoading] = useState(true);

  useEffect(() => {
    const controller = new AbortController();
    setFoundation({ isLoading: true });
    Promise.all([
      curatedRepository.getOverview(controller.signal),
      curatedRepository.getRelationships(controller.signal),
    ])
      .then(([overview, relationships]) => setFoundation({ overview, relationships, isLoading: false }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setFoundation({ error: messageFor(error), isLoading: false });
      });
    return () => controller.abort();
  }, [reloadKey]);

  useEffect(() => {
    const controller = new AbortController();
    setEntitiesLoading(true);
    setEntitiesError(undefined);
    curatedRepository
      .getEntities(type, search, page, controller.signal)
      .then((value) => {
        setEntities(value);
        setEntitiesLoading(false);
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setEntitiesError(messageFor(error));
          setEntitiesLoading(false);
        }
      });
    return () => controller.abort();
  }, [type, search, page, reloadKey]);

  const reload = useCallback(() => setReloadKey((value) => value + 1), []);

  return { ...foundation, entities, entitiesError, entitiesLoading, reload };
};

export const useAudit = (active: boolean) => {
  const [reloadKey, setReloadKey] = useState(0);
  const [data, setData] = useState<AuditSnapshot>();
  const [error, setError] = useState<string>();
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!active) return;
    const controller = new AbortController();
    setIsLoading(true);
    setError(undefined);
    curatedRepository
      .getAudit(controller.signal)
      .then((value) => {
        setData(value);
        setIsLoading(false);
      })
      .catch((caught: unknown) => {
        if (!controller.signal.aborted) {
          setError(messageFor(caught));
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [active, reloadKey]);

  return {
    data,
    error,
    isLoading,
    reload: useCallback(() => setReloadKey((value) => value + 1), []),
  };
};

export const useRaceResults = (active: boolean) => {
  const [reloadKey, setReloadKey] = useState(0);
  const [data, setData] = useState<RaceResultsFeed>();
  const [error, setError] = useState<string>();
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!active) return;
    const controller = new AbortController();
    setIsLoading(true);
    setError(undefined);
    curatedRepository
      .getResults({ signal: controller.signal })
      .then((value) => {
        setData(value);
        setIsLoading(false);
      })
      .catch((caught: unknown) => {
        if (!controller.signal.aborted) {
          setError(messageFor(caught));
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [active, reloadKey]);

  return {
    data,
    error,
    isLoading,
    reload: useCallback(() => setReloadKey((value) => value + 1), []),
  };
};

const monthRange = (month: string) => {
  const [year, monthNumber] = month.split("-").map(Number);
  const finalDay = new Date(Date.UTC(year, monthNumber, 0)).getUTCDate();
  return {
    from: `${month}-01`,
    to: `${month}-${String(finalDay).padStart(2, "0")}`,
  };
};

export const useRaceCalendar = (active: boolean) => {
  const [reloadKey, setReloadKey] = useState(0);
  const [month, setMonth] = useState<string>();
  const [data, setData] = useState<RaceResultsFeed>();
  const [error, setError] = useState<string>();
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!active) return;
    const controller = new AbortController();
    setIsLoading(true);
    setError(undefined);

    if (!month) {
      curatedRepository
        .getResults({ signal: controller.signal })
        .then((latest) => {
          if (!controller.signal.aborted) setMonth(latest.toDate.slice(0, 7));
        })
        .catch((caught: unknown) => {
          if (!controller.signal.aborted) {
            setError(messageFor(caught));
            setIsLoading(false);
          }
        });
      return () => controller.abort();
    }

    const range = monthRange(month);
    curatedRepository
      .getResults({ ...range, signal: controller.signal })
      .then((value) => {
        setData(value);
        setIsLoading(false);
      })
      .catch((caught: unknown) => {
        if (!controller.signal.aborted) {
          setError(messageFor(caught));
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [active, month, reloadKey]);

  return {
    month,
    setMonth,
    showLatest: useCallback(() => setMonth(undefined), []),
    data,
    error,
    isLoading,
    reload: useCallback(() => setReloadKey((value) => value + 1), []),
  };
};
