import type { MessageKey } from '../locales/en';

export type Role = 'PlatformOperator' | 'VenueManager' | 'DeskStaff';

export type Staff = {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  venueId: string | null;
  mustChangePassword: boolean;
};

export type StaffSummary = { id: string; email: string; displayName: string; role: Role; active: boolean };

export type Feature = { id: string; nameEn: string; nameAr: string };

export type Category = {
  id: string;
  nameEn: string;
  nameAr: string;
  iconKey: string;
  durationsMinutes: number[];
  sortOrder: number;
  active: boolean;
  features: Feature[];
};

export type VenueSummary = { id: string; nameEn: string; nameAr: string };

export type Day = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';
export const DAYS: Day[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

/** Times are "HH:mm:ss" on the wire. */
export type Hours = { day: Day; opens: string; closes: string };

export type Court = {
  id: string;
  categoryId: string;
  nameEn: string;
  nameAr: string;
  indoor: boolean;
  featureIds: string[];
  sortOrder: number;
  active: boolean;
};

export type CourtInput = Omit<Court, 'id'>;

export type MissingItem = 'address' | 'location' | 'hours' | 'courts';
export const CHECKLIST: MissingItem[] = ['address', 'location', 'hours', 'courts'];

export type Venue = {
  id: string;
  nameEn: string;
  nameAr: string;
  addressEn: string | null;
  addressAr: string | null;
  latitude: number | null;
  longitude: number | null;
  timeZoneId: string;
  published: boolean;
  hours: Hours[];
  courts: Court[];
  missingForPublish: MissingItem[];
};

export const ROLE_KEY: Record<Role, MessageKey> = {
  PlatformOperator: 'rolePlatformOperator',
  VenueManager: 'roleVenueManager',
  DeskStaff: 'roleDeskStaff',
};

export const DAY_KEY: Record<Day, MessageKey> = {
  Sunday: 'daySunday',
  Monday: 'dayMonday',
  Tuesday: 'dayTuesday',
  Wednesday: 'dayWednesday',
  Thursday: 'dayThursday',
  Friday: 'dayFriday',
  Saturday: 'daySaturday',
};

export const MISSING_KEY: Record<MissingItem, MessageKey> = {
  address: 'missingAddress',
  location: 'missingLocation',
  hours: 'missingHours',
  courts: 'missingCourts',
};
