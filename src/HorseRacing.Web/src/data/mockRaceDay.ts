import type { MeetingSummary, RaceDay, RaceStatus } from "../domain/racing";

const ids = {
  ascot: "11111111-1111-4111-8111-111111111111",
  pontefract: "22222222-2222-4222-8222-222222222222",
  stratford: "33333333-3333-4333-8333-333333333333",
  kempton: "44444444-4444-4444-8444-444444444444",
};

const race = (
  meetingId: string,
  date: string,
  raceNumber: number,
  localTime: string,
  name: string,
  code: MeetingSummary["races"][number]["code"],
  surface: MeetingSummary["races"][number]["surface"],
  distanceMetres: number,
  runnerCount: number,
  status: RaceStatus = "Scheduled",
) => ({
  id: `${meetingId.slice(0, 8)}-0000-4000-8000-${String(raceNumber).padStart(12, "0")}`,
  raceNumber,
  name,
  scheduledStartUtc: `${date}T${localTime}:00Z`,
  code,
  surface,
  distanceMetres,
  status,
  runnerCount,
});

const buildMeetings = (date: string): MeetingSummary[] => [
  {
    id: ids.ascot,
    name: "Autumn Racing Meeting",
    scheduledDate: date,
    type: "Flat",
    status: "InProgress",
    racecourse: { id: `${ids.ascot}-course`, name: "Ascot", locality: "Berkshire" },
    goingDescription: "Good to firm",
    weatherDescription: "Bright · 16°C",
    races: [
      race(ids.ascot, date, 1, "11:45", "The Opening Mile", "Flat", "Turf", 1609, 11, "Finished"),
      race(ids.ascot, date, 2, "12:20", "British EBF Novice Stakes", "Flat", "Turf", 1408, 9),
      race(ids.ascot, date, 3, "12:55", "Autumn Fillies' Handicap", "Flat", "Turf", 2012, 13),
      race(ids.ascot, date, 4, "13:30", "The Long Walk Handicap", "Flat", "Turf", 2414, 10),
      race(ids.ascot, date, 5, "14:05", "Ascot Members' Stakes", "Flat", "Turf", 1207, 14),
    ],
  },
  {
    id: ids.pontefract,
    name: "Season Finale",
    scheduledDate: date,
    type: "Flat",
    status: "Scheduled",
    racecourse: { id: `${ids.pontefract}-course`, name: "Pontefract", locality: "West Yorkshire" },
    goingDescription: "Good",
    weatherDescription: "Light cloud · 14°C",
    races: [
      race(ids.pontefract, date, 1, "12:05", "Apprentice Handicap", "Flat", "Turf", 1207, 12),
      race(ids.pontefract, date, 2, "12:40", "Maiden Fillies' Stakes", "Flat", "Turf", 1609, 8),
      race(ids.pontefract, date, 3, "13:15", "Silver Tankard Trial", "Flat", "Turf", 2012, 9),
      race(ids.pontefract, date, 4, "13:50", "West Yorkshire Sprint", "Flat", "Turf", 1006, 15),
    ],
  },
  {
    id: ids.stratford,
    name: "Monday Jumps",
    scheduledDate: date,
    type: "Jump",
    status: "Scheduled",
    racecourse: { id: `${ids.stratford}-course`, name: "Stratford", locality: "Warwickshire" },
    goingDescription: "Good, good to soft in places",
    weatherDescription: "Overcast · 13°C",
    races: [
      race(ids.stratford, date, 1, "12:30", "Conditional Jockeys' Hurdle", "Hurdle", "Turf", 3283, 10),
      race(ids.stratford, date, 2, "13:05", "Novices' Limited Handicap Chase", "Steeplechase", "Turf", 3912, 7),
      race(ids.stratford, date, 3, "13:40", "Mares' Handicap Hurdle", "Hurdle", "Turf", 3756, 12),
      race(ids.stratford, date, 4, "14:15", "National Hunt Flat Race", "NationalHuntFlat", "Turf", 3283, 9),
    ],
  },
  {
    id: ids.kempton,
    name: "Evening Racing",
    scheduledDate: date,
    type: "Flat",
    status: "Scheduled",
    racecourse: { id: `${ids.kempton}-course`, name: "Kempton Park", locality: "Surrey" },
    goingDescription: "Standard to slow",
    weatherDescription: "Indoor track · 15°C",
    races: [
      race(ids.kempton, date, 1, "16:15", "Unibet Nursery", "Flat", "AllWeather", 1408, 13),
      race(ids.kempton, date, 2, "16:50", "Fillies' Novice Stakes", "Flat", "AllWeather", 1609, 11),
      race(ids.kempton, date, 3, "17:25", "London Middle Distance Handicap", "Flat", "AllWeather", 2414, 8),
      race(ids.kempton, date, 4, "18:00", "Racing Club Sprint", "Flat", "AllWeather", 1207, 14),
    ],
  },
];

export const getMockRaceDay = (date: string, origin: RaceDay["origin"] = "mock"): RaceDay => ({
  date,
  generatedAtUtc: `${date}T10:52:00Z`,
  origin,
  notice:
    origin === "fallback"
      ? "The local read API is unavailable, so representative fixture data is shown."
      : "Representative fixture data is shown while the Created-layer read API is being built.",
  meetings: buildMeetings(date),
});

