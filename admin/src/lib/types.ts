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

/** A peak or off-peak period. Times are "HH:mm:ss"; to <= from runs past midnight. */
export type Band = { day: Day; from: string; to: string; hourlyPrice: number };

export type QuoteSegment = { from: string; to: string; hourlyPrice: number; amount: number };

export type Quote = {
  courtId: string;
  day: Day;
  start: string;
  minutes: number;
  total: number;
  net: number;
  vat: number;
  vatPercent: number;
  currency: string;
  segments: QuoteSegment[];
};

export type Court = {
  id: string;
  categoryId: string;
  nameEn: string;
  nameAr: string;
  indoor: boolean;
  hourlyPrice: number;
  featureIds: string[];
  sortOrder: number;
  active: boolean;
  bands: Band[];
};

export type CourtInput = Omit<Court, 'id' | 'bands'>;

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
  currencyCode: string;
  vatPercent: number;
  freeCancellationHours: number;
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

export type BookingKind = 'Booking' | 'Block';
export type BookingStatus = 'Confirmed' | 'Cancelled' | 'NoShow';

/** Times carry the venue's offset, e.g. "2026-11-19T20:00:00+03:00". phoneMasked is "•••• 1234" or null (walk-in, block). */
export type StaffBooking = {
  id: string;
  reference: string;
  kind: BookingKind;
  source: 'App' | 'Staff';
  courtId: string;
  courtNameEn: string;
  courtNameAr: string;
  startsAt: string;
  endsAt: string;
  minutes: number;
  total: number;
  currency: string;
  status: BookingStatus;
  customerId: string | null;
  customerName: string | null;
  phoneMasked: string | null;
  note: string | null;
  lateCancellation: boolean;
  cancelledByVenue: boolean;
  createdAt: string;
  checkedInAt: string | null;
  cashCollected: number | null;
  noShowAt: string | null;
};

export type ScheduleCourt = { id: string; categoryId: string; nameEn: string; nameAr: string; durationsMinutes: number[] };

/** One business day: from/to are the opening period (or the calendar day when closed). */
export type Schedule = {
  date: string;
  open: boolean;
  from: string;
  to: string;
  currency: string;
  courts: ScheduleCourt[];
  bookings: StaffBooking[];
};

export type VenueCustomer = {
  id: string;
  name: string | null;
  phoneMasked: string;
  bookings: number;
  cancellations: number;
  noShows: number;
  lastBookingAt: string;
};

export type CustomerLookup = { id: string; name: string | null; phoneMasked: string };

export type Today = {
  date: string;
  open: boolean;
  from: string;
  to: string;
  bookings: number;
  bookedMinutes: number;
  occupancyPercent: number;
  cashExpected: number;
  currency: string;
  cancelled: number;
  cashCollected: number;
  noShows: number;
  next: StaffBooking[];
};

export type HeatCell = { day: Day; hour: number; percent: number; bookedMinutes: number; openMinutes: number };
export type CashRow = { weekStart: string; courtId: string; courtNameEn: string; courtNameAr: string; cash: number; checkIns: number };
export type PlatformSettings = {
  reminderHoursBefore: number;
  noShowLimit: number;
  noShowWindowDays: number;
  limitedUpcomingBookings: number;
};

export type Report = {
  from: string;
  to: string;
  currency: string;
  dayStartHour: number;
  bookings: number;
  occupancyPercent: number;
  cashCollected: number;
  checkIns: number;
  played: number;
  noShows: number;
  noShowRatePercent: number;
  lateCancellations: number;
  venueCancellations: number;
  heatmap: HeatCell[];
  cash: CashRow[];
};

export type VenueLead = {
  id: string;
  venueName: string;
  contactName: string;
  phone: string;
  city: string;
  courts: number;
  message: string | null;
  language: string;
  createdAt: string;
  contactedAt: string | null;
};
