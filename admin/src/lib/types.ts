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

/** iconUrl is an uploaded icon ("/media/…"); it overrides the category's iconKey where shown. */
export type Feature = { id: string; nameEn: string; nameAr: string; iconUrl: string | null };

export type Category = {
  id: string;
  nameEn: string;
  nameAr: string;
  iconKey: string;
  durationsMinutes: number[];
  playersPerMatch: number;
  sortOrder: number;
  active: boolean;
  features: Feature[];
  iconUrl: string | null;
};

export type VenueSummary = { id: string; nameEn: string; nameAr: string; published: boolean; suspended: boolean };

/** A change an operator made while the venue was open. `at` is UTC. */
export type OperatorAction = { at: string; operatorName: string; method: string; path: string; status: number };

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
  logoUrl: string | null;
  photos: Photo[];
};

/** In display order; url is "/media/…". */
export type Photo = { id: string; url: string };

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
  /** Open match: the organiser is the booking's customer; `players` are the active joiners in join order. */
  match: StaffMatch | null;
  /** The organiser's points discount (owed to the venue by Midango) and the cash left to take at the desk. */
  pointsDiscount: number;
  amountToPay: number;
};

export type StaffMatchPlayer = {
  customerId: string;
  name: string | null;
  maskedPhone: string;
  joinedAt: string;
  checkedInAt: string | null;
  cashCollected: number | null;
  noShowAt: string | null;
  /** This player's points discount on their share, and the cash left to take. */
  pointsDiscount: number;
  amountToPay: number;
};

export type StaffMatch = {
  maxPlayers: number;
  level: string | null;
  share: number;
  organiserShare: number;
  players: StaffMatchPlayer[];
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
  /** Points discounts at today's check-ins: owed to the venue by Midango, not in cashCollected. */
  paidWithPoints: number;
};

export type HeatCell = { day: Day; hour: number; percent: number; bookedMinutes: number; openMinutes: number };
export type CashRow = {
  weekStart: string;
  courtId: string;
  courtNameEn: string;
  courtNameAr: string;
  cash: number;
  checkIns: number;
  /** Points discounts at check-in: owed by Midango, not in cash. */
  pointsDiscount: number;
};
export type PlatformSettings = {
  reminderHoursBefore: number;
  noShowLimit: number;
  noShowWindowDays: number;
  limitedUpcomingBookings: number;
  pointsPerSar: number;
  silverGames: number;
  goldGames: number;
  silverMultiplier: number;
  goldMultiplier: number;
  tierWindowDays: number;
  pointsPerRedeemStep: number;
  sarPerRedeemStep: number;
  maxRedeemPercent: number;
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
  pointsDiscount: number;
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

export type TournamentFormat = 'Knockout' | 'RoundRobin';
export type TournamentStatus = 'Draft' | 'Published' | 'InProgress' | 'Finished' | 'Cancelled';
export type TeamStatus = 'Registered' | 'Waitlisted' | 'Withdrawn';
export type PlayLevel = 'Beginner' | 'Intermediate' | 'Advanced';

/** Times carry the venue's offset, like bookings. */
export type TournamentSummary = {
  id: string;
  nameEn: string;
  nameAr: string;
  categoryId: string;
  format: TournamentFormat;
  status: TournamentStatus;
  startsAt: string;
  endsAt: string;
  registrationClosesAt: string;
  maxTeams: number;
  registered: number;
  waitlisted: number;
  entryFee: number;
  currency: string;
};

export type TournamentTeam = {
  id: string;
  name: string;
  status: TeamStatus;
  registeredAt: string;
  captain: { name: string; maskedPhone: string };
  members: string[];
  checkedInAt: string | null;
  cashCollected: number | null;
  /** Set by the draw; groupNo only in round robin. */
  drawPosition: number | null;
  groupNo: number | null;
};

/** The create/update body; the detail carries the same fields. */
export type TournamentInput = {
  categoryId: string;
  nameEn: string;
  nameAr: string;
  descriptionEn: string | null;
  descriptionAr: string | null;
  format: TournamentFormat;
  teamSize: number;
  maxTeams: number;
  entryFee: number;
  level: PlayLevel | null;
  courtIds: string[];
  startsAt: string;
  endsAt: string;
  registrationClosesAt: string;
};

export type Tournament = TournamentInput & {
  id: string;
  currency: string;
  status: TournamentStatus;
  courts: { id: string; nameEn: string; nameAr: string }[];
  teams: TournamentTeam[];
  matchMinutes: number | null;
  groupCount: number | null;
  drawnAt: string | null;
  championTeamId: string | null;
  /** The drawn schedule runs past the tournament's end. */
  overrunsEnd: boolean;
  matches: TournamentMatch[];
  groups: TournamentGroup[];
};

/** A side of a match: null until the feeding match is decided. */
export type MatchTeam = { id: string; name: string };

export type TournamentMatch = {
  id: string;
  stage: 'Knockout' | 'Group';
  round: number;
  position: number;
  groupNo: number | null;
  teamA: MatchTeam | null;
  teamB: MatchTeam | null;
  scoreA: number | null;
  scoreB: number | null;
  winnerTeamId: string | null;
  isBye: boolean;
  courtId: string | null;
  courtNameEn: string | null;
  courtNameAr: string | null;
  startsAt: string | null;
  nextMatchId: string | null;
  nextSlot: 'A' | 'B' | null;
};

export type GroupStanding = {
  teamId: string;
  name: string;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  scoreFor: number;
  scoreAgainst: number;
  points: number;
};

/** Standings arrive already ordered. */
export type TournamentGroup = { groupNo: number; standings: GroupStanding[] };

/** A publish 409 lists the bookings in the way. */
export type TournamentClash = { courtNameEn: string; courtNameAr: string; startsAt: string; endsAt: string; reference: string };

export const FORMAT_KEY: Record<TournamentFormat, MessageKey> = { Knockout: 'formatKnockout', RoundRobin: 'formatRoundRobin' };
export const TOURNAMENT_STATUS_KEY: Record<TournamentStatus, MessageKey> = {
  Draft: 'tournamentDraft',
  Published: 'tournamentPublished',
  InProgress: 'tournamentInProgress',
  Finished: 'tournamentFinished',
  Cancelled: 'tournamentCancelled',
};
export const LEVEL_KEY: Record<PlayLevel, MessageKey> = {
  Beginner: 'levelBeginner',
  Intermediate: 'levelIntermediate',
  Advanced: 'levelAdvanced',
};
