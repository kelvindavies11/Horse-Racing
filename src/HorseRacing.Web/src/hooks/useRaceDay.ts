import { useCallback, useEffect, useState } from "react";
import { racingRepository } from "../data/racingRepository";
import type { RaceDay } from "../domain/racing";

interface RaceDayState {
  data?: RaceDay;
  error?: string;
  isLoading: boolean;
}

export const useRaceDay = (date: string) => {
  const [reloadKey, setReloadKey] = useState(0);
  const [state, setState] = useState<RaceDayState>({ isLoading: true });

  useEffect(() => {
    const controller = new AbortController();
    setState({ isLoading: true });

    racingRepository
      .getRaceDay(date, controller.signal)
      .then((data) => setState({ data, isLoading: false }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        setState({
          error: error instanceof Error ? error.message : "Unable to load racing data.",
          isLoading: false,
        });
      });

    return () => controller.abort();
  }, [date, reloadKey]);

  const reload = useCallback(() => setReloadKey((key) => key + 1), []);

  return { ...state, reload };
};

