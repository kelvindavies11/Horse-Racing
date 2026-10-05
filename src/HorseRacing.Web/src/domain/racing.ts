export type DataOrigin = "api" | "mock" | "fallback";
export type MeetingType = "Flat" | "Jump" | "Mixed";
export type MeetingStatus = "Scheduled" | "InProgress" | "Completed" | "Abandoned";
export type RaceCode = "Flat" | "Hurdle" | "Steeplechase" | "NationalHuntFlat";
export type RacingSurface = "Turf" | "AllWeather";
export type RaceStatus = "Scheduled" | "Off" | "Finished" | "Abandoned";

export interface RaceSummary {
  id: string;
  raceNumber: number;
  name: string;
  scheduledStartUtc: string;
  code: RaceCode;
  surface: RacingSurface;
  distanceMetres: number;
  status: RaceStatus;
  runnerCount: number;
}

export interface MeetingSummary {
  id: string;
  name: string;
  scheduledDate: string;
  type: MeetingType;
  status: MeetingStatus;
  racecourse: {
    id: string;
    name: string;
    locality: string;
  };
  goingDescription: string;
  weatherDescription: string;
  races: RaceSummary[];
}

export interface RaceDay {
  date: string;
  generatedAtUtc: string;
  origin: DataOrigin;
  notice?: string;
  meetings: MeetingSummary[];
}

export interface RacingRepository {
  getRaceDay(date: string, signal?: AbortSignal): Promise<RaceDay>;
}

