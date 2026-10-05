import type { RaceDay, RacingRepository } from "../domain/racing";
import { getMockRaceDay } from "./mockRaceDay";

export class RacingApiError extends Error {
  constructor(
    message: string,
    readonly status?: number,
  ) {
    super(message);
    this.name = "RacingApiError";
  }
}

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "/api").replace(/\/$/, "");
const DATA_MODE = import.meta.env.VITE_DATA_MODE ?? "mock";

const readApiRaceDay = async (date: string, signal?: AbortSignal): Promise<RaceDay> => {
  const response = await fetch(`${API_BASE_URL}/v1/race-days/${date}`, {
    headers: { Accept: "application/json" },
    signal,
  });

  if (!response.ok) {
    throw new RacingApiError(`Unable to load racing data (${response.status}).`, response.status);
  }

  const payload = (await response.json()) as RaceDay;

  if (!payload || payload.date !== date || !Array.isArray(payload.meetings)) {
    throw new RacingApiError("The racing API returned an unexpected response.");
  }

  return { ...payload, origin: "api" };
};

export const racingRepository: RacingRepository = {
  async getRaceDay(date, signal) {
    if (DATA_MODE === "mock") {
      return getMockRaceDay(date);
    }

    try {
      return await readApiRaceDay(date, signal);
    } catch (error) {
      if (signal?.aborted) {
        throw error;
      }

      if (DATA_MODE === "auto") {
        return getMockRaceDay(date, "fallback");
      }

      throw error;
    }
  },
};

