import type { Lang } from './i18n';

/** Rows on the schedule and the API's start-time grid. */
export const STEP = 30;

/** Venue times arrive with the venue's offset ("2026-11-19T20:00:00+03:00"). Reading the wall-clock
 *  part straight from the string shows venue time whatever time zone the browser is in. */
export const clock = (iso: string) => iso.slice(11, 16);
export const dayOf = (iso: string) => iso.slice(0, 10);

function offsetMinutes(iso: string): number {
  const m = /([+-])(\d{2}):(\d{2})$/.exec(iso);
  if (!m) return 0; // "Z"
  return (m[1] === '-' ? -1 : 1) * (Number(m[2]) * 60 + Number(m[3]));
}

/** The same instant format and offset, `minutes` later. */
export function shift(iso: string, minutes: number): string {
  const suffix = iso.endsWith('Z') ? 'Z' : iso.slice(-6);
  const wall = new Date(new Date(iso).getTime() + (minutes + offsetMinutes(iso)) * 60_000);
  return wall.toISOString().slice(0, 19) + suffix;
}

export const minutesBetween = (a: string, b: string) =>
  Math.round((new Date(b).getTime() - new Date(a).getTime()) / 60_000);

/** Every half-hour start from `from` while it is before `to`. */
export function slotStarts(from: string, to: string): string[] {
  return Array.from({ length: Math.floor(minutesBetween(from, to) / STEP) }, (_, k) => shift(from, k * STEP));
}

/** "YYYY-MM-DD" plus n days (UTC arithmetic: no DST drift). */
export function addDays(date: string, n: number): string {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
}

/** "Thursday 19 November" / "الخميس 19 نوفمبر": Gregorian with Western digits in Arabic too. */
export function formatDay(date: string, lang: Lang, style: 'long' | 'short' = 'long'): string {
  return new Intl.DateTimeFormat(lang === 'ar' ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB', {
    weekday: style,
    day: 'numeric',
    month: style,
    timeZone: 'UTC',
  }).format(new Date(`${date}T12:00:00Z`));
}

/** Within the API's 15-minute undo window. */
export const recent = (iso: string | null) => iso !== null && Date.now() - new Date(iso).getTime() < 15 * 60_000;
