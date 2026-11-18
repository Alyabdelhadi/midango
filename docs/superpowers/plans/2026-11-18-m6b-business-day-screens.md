# M6b Business Day Screens Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Venue managers and desk staff run the day in Midango Business: a **Today** home page, a **Schedule** grid (courts × half hours; click a free time to book or block it, drag a booking to move it, open any booking to move or cancel it), a **Bookings** list with quick filters and search, a **Customers** page with masked phones, and the free-cancellation setting on the Venue page. English and Arabic (RTL).

**Architecture:** Screens only; everything they need is in the M6a API (one tiny API change: the schedule's date becomes optional and defaults to the venue's business day). Venue times arrive as ISO strings carrying the venue offset (`2026-11-19T20:00:00+03:00`); the UI reads the wall-clock part straight from the string (`lib/time.ts`), so the grid shows venue time whatever zone the browser is in. The grid is one CSS grid: column 1 = time labels, one column per court, one row per 30 minutes; bookings are placed over the empty cells with `grid-row: start / end`. Drag-and-drop is native HTML5; the booking panel's Move form is the keyboard path.

**Tech Stack:** React 19 · react-router 7 · TypeScript · Vite 8 · lucide-react · oxlint. No new dependencies.

**Project rules:** No automated tests; Task 5 is a browser walkthrough by the controller. Every user-facing string goes through `t()` with an English and an Arabic entry (the Arabic dictionary is typed, so a missing key fails the build). Brand: lime is a fill with navy text only; links/focus use `--mg-cyan-ink`; status always in words, never colour alone. Build and lint from `admin/` with `npm run build` and `npm run lint`.

**Decisions this plan locks in:**

| Topic | Decision | Why |
|---|---|---|
| Home for venue staff | `/today` (operator still lands on `/categories`) | The first question at a desk is "what's happening now" |
| Today refresh | Reloads every 60 s | Desk screen stays current without a button |
| Grid rows | 30-minute rows over the business day (16:00 → 02:00); closed day shows a notice | Same grid as the API's start times |
| Free cells | Mouse targets only (`tabIndex=-1`); keyboard users use the header **New booking** button | 60+ tab stops per day would bury the bookings |
| Lengths offered at the desk | Every 30 minutes up to closing (max 12 h); default = the sport's first length | Staff rule from M6a |
| Phone at the desk | Optional; on leaving the field the UI looks the number up and shows "Booked here before as …" or "New customer" | Faster repeat bookings; still never shows the number back |
| Move | Drag a booking onto a free cell (new start = that cell), or the panel's Move form (court + start) | Mouse and keyboard paths |
| Cancel / remove block | Two-step confirm inside the panel | Destructive, not undoable |
| Bookings filters | Today · Next 7 days · Cancelled (±30 days) · Dates (custom); search by name, reference or phone digits | Matches the API's 62-day range |
| Dates in Arabic | Gregorian calendar, Western digits (`ar-SA-u-ca-gregory-nu-latn`) | `ar-SA` defaults to the Hijri calendar; times and amounts already use Western digits |

## File structure

- Modify `api/src/Midango.Api/Bookings/StaffBookingEndpoints.cs` — `GET /venue/schedule` date optional.
- Modify `admin/src/lib/types.ts` — booking, schedule, customer, today types; `Venue.freeCancellationHours`.
- Create `admin/src/lib/time.ts` — venue-time string helpers.
- Create `admin/src/lib/useToast.tsx` — the success/error toast used by the new pages.
- Modify `admin/src/locales/en.ts`, `admin/src/locales/ar.ts` — new strings.
- Create `admin/src/components/BookingStatusPill.tsx`.
- Create `admin/src/pages/TodayPage.tsx`.
- Create `admin/src/pages/schedule/SchedulePage.tsx`, `NewBookingDialog.tsx`, `BookingPanel.tsx`.
- Create `admin/src/pages/BookingsPage.tsx`, `admin/src/pages/CustomersPage.tsx`.
- Modify `admin/src/pages/venue/DetailsSection.tsx` — free-cancellation hours.
- Modify `admin/src/App.tsx`, `admin/src/app/guards.tsx`, `admin/src/layout/Shell.tsx` — routes, home, nav.
- Modify `admin/src/ui.css` — Today, schedule, panel, filters.

---

### Task 1: Foundations — API default date, types, time helpers, toast, strings, venue setting, nav and routes

- [ ] **Step 1: Schedule date optional (API).** In `StaffBookingEndpoints.cs`, change the schedule handler's signature and first lines to:

```csharp
        venueStaff.MapGet("/schedule", async (DateOnly? date, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var zone = VenueClock.Zone(venue);
            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venue.Id).ToListAsync(ct);
            // No date = the venue's current business day (01:00 still belongs to last night).
            var day = date ?? Slots.BusinessDay(VenueClock.ToLocal(clock.GetUtcNow(), zone), hours);
            var (fromLocal, toLocal) = Slots.DayWindow(day, hours);
```

and use `day` instead of `date` in the rest of the handler (`Slots.Period(day, hours)` and `new ScheduleDto(day, …)`). `dotnet build` in `api/` → 0 warnings, 0 errors.

- [ ] **Step 2: Types.** In `admin/src/lib/types.ts` add `freeCancellationHours: number;` to `Venue` (after `vatPercent`) and append:

```ts
export type BookingKind = 'Booking' | 'Block';
export type BookingStatus = 'Confirmed' | 'Cancelled';

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
  next: StaffBooking[];
};
```

- [ ] **Step 3: Time helpers.** Create `admin/src/lib/time.ts`:

```ts
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
```

- [ ] **Step 4: Toast.** Create `admin/src/lib/useToast.tsx`:

```tsx
import { useCallback, useEffect, useState } from 'react';
import { Notice } from '../components/Notice';

type Toast = { tone: 'error' | 'success'; text: string };

/** Success messages fade after 5 s; errors stay until the next action. Render `view` once per page. */
export function useToast() {
  const [toast, setToast] = useState<Toast>();

  useEffect(() => {
    if (toast?.tone !== 'success') return;
    const timer = setTimeout(() => setToast(undefined), 5000);
    return () => clearTimeout(timer);
  }, [toast]);

  const show = useCallback((tone: Toast['tone'], text: string) => setToast({ tone, text }), []);
  const view = (
    <div className="toast-region" aria-live="polite">
      {toast && <Notice tone={toast.tone}>{toast.text}</Notice>}
    </div>
  );
  return { show, view };
}
```

- [ ] **Step 5: Strings.** Add to `en` in `admin/src/locales/en.ts` (before the closing `};`):

```ts
  navToday: 'Today',
  navSchedule: 'Schedule',
  navBookings: 'Bookings',
  navCustomers: 'Customers',
  tileBookings: 'Bookings',
  tileOccupancy: 'Courts booked',
  tileCash: 'Cash expected',
  tileCancelled: 'Cancelled',
  nowAndNext: 'Now and next',
  nothingNext: 'Nothing else booked today.',
  closedToday: 'Closed today.',
  openSchedule: 'Open schedule',
  playingNow: 'Playing now',
  previousDay: 'Previous day',
  nextDay: 'Next day',
  date: 'Date',
  closedOnDay: 'The venue is closed on this day.',
  scheduleNoCourts: 'Add a court on the Venue page to start taking bookings.',
  scheduleHint: 'Click a free time to book or block it. Drag a booking to move it.',
  bookCourtAt: 'Book {court} at {time}',
  blocked: 'Blocked',
  newBooking: 'New booking',
  modeBooking: 'Booking',
  modeBlock: 'Block time',
  customerName: 'Customer name',
  phone: 'Mobile',
  phoneOptional: 'Mobile number (optional)',
  phoneHint: 'Leave empty for a walk-in. Staff only ever see the last 4 digits.',
  knownCustomer: 'Booked here before as {name}.',
  newCustomer: 'New customer: they can sign in to the app with this number to see the booking.',
  note: 'Note',
  noteHint: 'Only staff see this.',
  book: 'Book',
  block: 'Block',
  bookedNotice: 'Booked. Reference {ref}.',
  blockedNotice: 'Time blocked.',
  movedNotice: 'Booking moved.',
  cancelledNotice: 'Booking cancelled.',
  removedBlockNotice: 'Block removed.',
  bookingTitle: 'Booking {ref}',
  reference: 'Reference',
  customer: 'Customer',
  time: 'Time',
  price: 'Price',
  cashAtVenue: 'Cash at the venue',
  source: 'Booked by',
  sourceApp: 'Customer, in the app',
  sourceStaff: 'Staff',
  walkIn: 'Walk-in',
  statusConfirmed: 'Confirmed',
  statusCancelled: 'Cancelled',
  statusCancelledByVenue: 'Cancelled by venue',
  statusLateCancel: 'Late cancellation',
  moveTo: 'Move to',
  moveBooking: 'Move',
  pickNewPlace: 'Pick a different court or time first.',
  cancelBooking: 'Cancel booking',
  removeBlock: 'Remove block',
  confirmCancel: 'Cancel this booking? The customer sees it was cancelled by the venue.',
  confirmRemoveBlock: 'Remove this block? The time goes back on sale.',
  keepIt: 'Keep it',
  yesCancel: 'Yes, cancel',
  yesRemove: 'Yes, remove',
  close: 'Close',
  finishedNote: 'This booking has finished.',
  filterWeek: 'Next 7 days',
  filterDates: 'Dates',
  search: 'Search',
  searchBookingsHint: 'Name, reference or phone digits',
  noBookings: 'No bookings match.',
  customersIntro: 'People who booked here with a mobile number. Phones show the last 4 digits only.',
  searchCustomersHint: 'Name or phone digits',
  noCustomers: 'No customers yet.',
  noName: 'No name',
  cancellations: 'Cancellations',
  lastBooking: 'Last booking',
  freeCancellationHours: 'Free cancellation (hours before start)',
  freeCancellationHint: 'Customers who cancel later are marked as late.',
```

and to `ar` in `admin/src/locales/ar.ts`:

```ts
  navToday: 'اليوم',
  navSchedule: 'الجدول',
  navBookings: 'الحجوزات',
  navCustomers: 'العملاء',
  tileBookings: 'الحجوزات',
  tileOccupancy: 'إشغال الملاعب',
  tileCash: 'النقد المتوقع',
  tileCancelled: 'الملغاة',
  nowAndNext: 'الآن والتالي',
  nothingNext: 'لا توجد حجوزات أخرى اليوم.',
  closedToday: 'مغلق اليوم.',
  openSchedule: 'فتح الجدول',
  playingNow: 'يلعب الآن',
  previousDay: 'اليوم السابق',
  nextDay: 'اليوم التالي',
  date: 'التاريخ',
  closedOnDay: 'المنشأة مغلقة في هذا اليوم.',
  scheduleNoCourts: 'أضف ملعباً من صفحة المنشأة لبدء استقبال الحجوزات.',
  scheduleHint: 'انقر على وقت متاح لحجزه أو إغلاقه. اسحب الحجز لنقله.',
  bookCourtAt: 'احجز {court} الساعة {time}',
  blocked: 'مغلق',
  newBooking: 'حجز جديد',
  modeBooking: 'حجز',
  modeBlock: 'إغلاق وقت',
  customerName: 'اسم العميل',
  phone: 'الجوال',
  phoneOptional: 'رقم الجوال (اختياري)',
  phoneHint: 'اتركه فارغاً للعميل الحاضر دون حجز. يرى الموظفون آخر 4 أرقام فقط.',
  knownCustomer: 'حجز هنا من قبل باسم {name}.',
  newCustomer: 'عميل جديد: يمكنه الدخول إلى التطبيق بهذا الرقم لرؤية الحجز.',
  note: 'ملاحظة',
  noteHint: 'يراها الموظفون فقط.',
  book: 'احجز',
  block: 'أغلق الوقت',
  bookedNotice: 'تم الحجز. المرجع {ref}.',
  blockedNotice: 'تم إغلاق الوقت.',
  movedNotice: 'تم نقل الحجز.',
  cancelledNotice: 'تم إلغاء الحجز.',
  removedBlockNotice: 'تمت إزالة الإغلاق.',
  bookingTitle: 'الحجز {ref}',
  reference: 'المرجع',
  customer: 'العميل',
  time: 'الوقت',
  price: 'السعر',
  cashAtVenue: 'نقداً في المنشأة',
  source: 'جهة الحجز',
  sourceApp: 'العميل عبر التطبيق',
  sourceStaff: 'الموظفون',
  walkIn: 'حضور مباشر',
  statusConfirmed: 'مؤكد',
  statusCancelled: 'ملغى',
  statusCancelledByVenue: 'ألغته المنشأة',
  statusLateCancel: 'إلغاء متأخر',
  moveTo: 'نقل إلى',
  moveBooking: 'نقل',
  pickNewPlace: 'اختر ملعباً أو وقتاً مختلفاً أولاً.',
  cancelBooking: 'إلغاء الحجز',
  removeBlock: 'إزالة الإغلاق',
  confirmCancel: 'هل تريد إلغاء هذا الحجز؟ سيرى العميل أن المنشأة ألغته.',
  confirmRemoveBlock: 'هل تريد إزالة الإغلاق؟ سيعود الوقت متاحاً للحجز.',
  keepIt: 'إبقاء',
  yesCancel: 'نعم، ألغِ',
  yesRemove: 'نعم، أزِل',
  close: 'إغلاق',
  finishedNote: 'انتهى هذا الحجز.',
  filterWeek: 'الأيام السبعة القادمة',
  filterDates: 'تواريخ',
  search: 'بحث',
  searchBookingsHint: 'الاسم أو المرجع أو أرقام الجوال',
  noBookings: 'لا توجد حجوزات مطابقة.',
  customersIntro: 'الأشخاص الذين حجزوا هنا برقم جوال. تظهر آخر 4 أرقام فقط.',
  searchCustomersHint: 'الاسم أو أرقام الجوال',
  noCustomers: 'لا يوجد عملاء بعد.',
  noName: 'بدون اسم',
  cancellations: 'الإلغاءات',
  lastBooking: 'آخر حجز',
  freeCancellationHours: 'الإلغاء المجاني (ساعات قبل البدء)',
  freeCancellationHint: 'العملاء الذين يلغون بعد ذلك يُسجَّل إلغاؤهم متأخراً.',
```

- [ ] **Step 6: Status pill.** Create `admin/src/components/BookingStatusPill.tsx`:

```tsx
import { useI18n } from '../lib/i18n';
import type { StaffBooking } from '../lib/types';
import { Pill } from './Pill';

export function BookingStatusPill({ booking }: { booking: StaffBooking }) {
  const { t } = useI18n();
  if (booking.status === 'Confirmed') return <Pill tone="success">{t('statusConfirmed')}</Pill>;
  if (booking.cancelledByVenue) return <Pill>{t('statusCancelledByVenue')}</Pill>;
  if (booking.lateCancellation) return <Pill tone="warning">{t('statusLateCancel')}</Pill>;
  return <Pill>{t('statusCancelled')}</Pill>;
}
```

- [ ] **Step 7: Free-cancellation field.** In `pages/venue/DetailsSection.tsx`:
  - `DetailsInput` gains `freeCancellationHours: number;`.
  - The draft gains `freeCancellationHours: String(venue.freeCancellationHours),`.
  - `submit` passes `freeCancellationHours: Number(draft.freeCancellationHours),`.
  - Add this field after the longitude field, inside the same fieldset:

```tsx
          <Field label={t('freeCancellationHours')} hint={t('freeCancellationHint')}>
            <input
              type="number"
              min={0}
              max={168}
              step={1}
              required
              inputMode="numeric"
              value={draft.freeCancellationHours}
              onChange={(e) => set('freeCancellationHours')(e.target.value)}
            />
          </Field>
```

- [ ] **Step 8: Nav, home and routes.**
  - `layout/Shell.tsx`: import `CalendarDays, Contact, LayoutDashboard, ListChecks` from `lucide-react` and put these first in `NAV` (before Sport categories):

```tsx
  { to: '/today', label: 'navToday', icon: LayoutDashboard, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/schedule', label: 'navSchedule', icon: CalendarDays, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/bookings', label: 'navBookings', icon: ListChecks, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/customers', label: 'navCustomers', icon: Contact, roles: ['VenueManager', 'DeskStaff'] },
```

  - `app/guards.tsx` `HomeRedirect`: `'/venue'` becomes `'/today'`.
  - `App.tsx`: import the four pages and add, inside the `<Shell />` route next to `venue`:

```tsx
                {(
                  [
                    ['today', <TodayPage />],
                    ['schedule', <SchedulePage />],
                    ['bookings', <BookingsPage />],
                    ['customers', <CustomersPage />],
                  ] as const
                ).map(([path, page]) => (
                  <Route
                    key={path}
                    path={path}
                    element={<RequireRole roles={['VenueManager', 'DeskStaff']}>{page}</RequireRole>}
                  />
                ))}
```

  Until Tasks 2–4 exist, create each page file as a one-line placeholder export (`export function TodayPage() { return null; }` etc.) so the build passes; the following tasks replace them. Paths: `pages/TodayPage.tsx`, `pages/schedule/SchedulePage.tsx`, `pages/BookingsPage.tsx`, `pages/CustomersPage.tsx`.

- [ ] **Step 9: Styles.** Append to `admin/src/ui.css`:

```css
/* Shared */
.muted { color: var(--mg-muted); }
[dir='rtl'] .flip-rtl { transform: scaleX(-1); }
.details { display: grid; grid-template-columns: max-content 1fr; gap: 8px 16px; margin: 0; }
.details dt { font: var(--mg-text-label); color: var(--mg-muted); }
.details dd { margin: 0; color: var(--mg-navy); }
.panel-section { display: grid; gap: 12px; padding-block-start: 12px; border-block-start: 1px solid var(--mg-line); }
.filter-bar { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 12px 16px; }
.search-form { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 8px; }
.search-form .mg-field { width: min(320px, 100%); }

/* Today */
.tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; }
.tile { display: grid; gap: 4px; padding: 16px; background: var(--mg-white); border: 1px solid var(--mg-line); border-radius: 12px; }
.tile-label { font: var(--mg-text-label); color: var(--mg-muted); }
.tile-value { font: var(--mg-text-display); color: var(--mg-navy); }
.next-list { display: grid; margin: 0; padding: 0; list-style: none; }
.next-list li { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 16px; padding-block: 10px; border-block-end: 1px solid var(--mg-line); }
.next-list li:last-child { border-block-end: 0; }
.next-time { min-width: 104px; font: var(--mg-text-numeric); color: var(--mg-navy); }

/* Schedule */
.page--wide { max-width: none; }
.day-nav { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.schedule-scroll { overflow: auto; max-height: calc(100vh - 220px); background: var(--mg-white); border: 1px solid var(--mg-line); border-radius: 12px; }
.schedule-grid { display: grid; min-width: max-content; }
.schedule-corner, .schedule-court { position: sticky; top: 0; z-index: 3; background: var(--mg-cloud); border-block-end: 1px solid var(--mg-line); }
.schedule-corner { inset-inline-start: 0; z-index: 4; }
.schedule-court { display: flex; align-items: center; padding-inline: 10px; font: var(--mg-text-label); color: var(--mg-navy); }
.schedule-time { position: sticky; inset-inline-start: 0; z-index: 2; padding-inline: 8px; background: var(--mg-white); font: var(--mg-text-caption); color: var(--mg-muted); }
.schedule-cell { padding: 0; background: transparent; border: 0; border-block-start: 1px solid var(--mg-line); border-inline-start: 1px solid var(--mg-line); cursor: pointer; }
.schedule-cell.on-hour { border-block-start-color: #c9d0db; }
.schedule-cell:hover, .schedule-cell.drop-target { background: var(--mg-cloud); }
.schedule-booking {
  z-index: 1; display: grid; align-content: start; gap: 2px; margin: 2px; padding: 4px 8px; overflow: hidden;
  background: var(--mg-lime); color: var(--mg-navy); border: 0; border-radius: 6px;
  font: var(--mg-text-caption); text-align: start; cursor: pointer;
}
.schedule-booking[draggable='true'] { cursor: grab; }
.schedule-booking--block {
  background: repeating-linear-gradient(135deg, var(--mg-disabled-bg) 0 8px, var(--mg-white) 8px 16px);
  color: var(--mg-slate); border: 1px solid #c9d0db;
}
.schedule-booking:focus-visible { outline: 3px solid var(--mg-cyan-ink); outline-offset: 1px; }
.schedule-booking-time { font: var(--mg-text-numeric); font-size: 12px; }
```

- [ ] **Step 10: Build and lint.** In `admin/`: `npm run build` and `npm run lint` → no errors. **Commit** `feat(admin): today/schedule/bookings/customers routes, venue-time helpers, strings, free-cancellation setting` (include the API change).

---

### Task 2: Today page

- [ ] **Step 1:** Replace `admin/src/pages/TodayPage.tsx`:

```tsx
import { useEffect } from 'react';
import { Link } from 'react-router';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { clock, formatDay } from '../lib/time';
import type { Today } from '../lib/types';
import { useLoad } from '../lib/useLoad';

export function TodayPage() {
  const { t, lang, name, money } = useI18n();
  const today = useLoad(() => api<Today>('/venue/today'));

  // A desk screen stays open all evening: keep the numbers current.
  useEffect(() => {
    const timer = setInterval(today.reload, 60_000);
    return () => clearInterval(timer);
  }, [today.reload]);

  if (today.error) {
    return (
      <div className="page">
        <Notice tone="error">{today.error}</Notice>
      </div>
    );
  }
  if (!today.data) return <p className="page-loading">{t('loading')}</p>;
  const d = today.data;
  const now = Date.now();

  return (
    <div className="page">
      <PageHeader
        title={t('navToday')}
        action={
          <Link className="mg-button mg-button--secondary" to="/schedule">
            {t('openSchedule')}
          </Link>
        }
      />
      <p className="page-intro">
        {formatDay(d.date, lang)}
        {d.open && ` · ${clock(d.from)}–${clock(d.to)}`}
      </p>
      {!d.open && <Notice tone="info">{t('closedToday')}</Notice>}
      <div className="tiles">
        <Tile label={t('tileBookings')} value={String(d.bookings)} />
        <Tile label={t('tileOccupancy')} value={`${d.occupancyPercent}%`} />
        <Tile label={t('tileCash')} value={money(d.cashExpected, d.currency)} />
        <Tile label={t('tileCancelled')} value={String(d.cancelled)} />
      </div>
      <section className="card">
        <h2>{t('nowAndNext')}</h2>
        {d.next.length === 0 ? (
          <p className="empty">{t('nothingNext')}</p>
        ) : (
          <ul className="next-list">
            {d.next.map((b) => (
              <li key={b.id}>
                <span className="next-time">
                  {clock(b.startsAt)}–{clock(b.endsAt)}
                </span>
                <span>{name({ nameEn: b.courtNameEn, nameAr: b.courtNameAr })}</span>
                <span>
                  {b.customerName}{' '}
                  <bdi dir="ltr" className="muted">
                    {b.phoneMasked ?? t('walkIn')}
                  </bdi>
                </span>
                {new Date(b.startsAt).getTime() <= now && <Pill tone="success">{t('playingNow')}</Pill>}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function Tile({ label, value }: { label: string; value: string }) {
  return (
    <div className="tile">
      <span className="tile-label">{label}</span>
      <span className="tile-value">{value}</span>
    </div>
  );
}
```

- [ ] **Step 2:** `npm run build`, `npm run lint` → clean. **Commit** `feat(admin): Today page with the day's numbers and now-and-next`.

---

### Task 3: Schedule — grid, new booking / block dialog, booking panel

- [ ] **Step 1: Booking panel.** Create `admin/src/pages/schedule/BookingPanel.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { BookingStatusPill } from '../../components/BookingStatusPill';
import { Button } from '../../components/Button';
import { Dialog } from '../../components/Dialog';
import { Field } from '../../components/Field';
import { Notice } from '../../components/Notice';
import { api, errorText } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import { clock, dayOf, formatDay, minutesBetween, slotStarts } from '../../lib/time';
import type { Schedule, StaffBooking } from '../../lib/types';

/** One booking or block: details, Move (when that day's schedule is at hand) and Cancel / Remove. */
export function BookingPanel({
  booking,
  schedule,
  onDone,
  onClose,
}: {
  booking: StaffBooking | undefined;
  schedule?: Schedule;
  onDone: (message: string) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const title = booking?.kind === 'Block' ? t('blocked') : t('bookingTitle', { ref: booking?.reference ?? '' });
  return (
    <Dialog open={booking !== undefined} title={title} onClose={onClose}>
      {booking && <PanelBody booking={booking} schedule={schedule} onDone={onDone} onClose={onClose} />}
    </Dialog>
  );
}

function PanelBody({
  booking: b,
  schedule,
  onDone,
  onClose,
}: {
  booking: StaffBooking;
  schedule?: Schedule;
  onDone: (message: string) => void;
  onClose: () => void;
}) {
  const { t, lang, name, money } = useI18n();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const isBlock = b.kind === 'Block';
  const live = b.status === 'Confirmed' && new Date(b.endsAt).getTime() > Date.now();

  async function act(path: string, method: 'POST' | 'PUT', body: unknown, message: string) {
    setBusy(true);
    setError(undefined);
    try {
      await api(path, { method, body });
      onDone(message);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="dialog-form">
      <dl className="details">
        <dt>{t('time')}</dt>
        <dd>
          {formatDay(dayOf(b.startsAt), lang)} · {clock(b.startsAt)}–{clock(b.endsAt)} ({t('minutesShort', { m: b.minutes })})
        </dd>
        <dt>{t('court')}</dt>
        <dd>{name({ nameEn: b.courtNameEn, nameAr: b.courtNameAr })}</dd>
        {!isBlock && (
          <>
            <dt>{t('customer')}</dt>
            <dd>
              {b.customerName}{' '}
              <bdi dir="ltr" className="muted">
                {b.phoneMasked ?? t('walkIn')}
              </bdi>
            </dd>
            <dt>{t('price')}</dt>
            <dd>
              {money(b.total, b.currency)} · {t('cashAtVenue')}
            </dd>
            <dt>{t('source')}</dt>
            <dd>{t(b.source === 'App' ? 'sourceApp' : 'sourceStaff')}</dd>
            <dt>{t('status')}</dt>
            <dd>
              <BookingStatusPill booking={b} />
            </dd>
          </>
        )}
        {b.note && (
          <>
            <dt>{t('note')}</dt>
            <dd>{b.note}</dd>
          </>
        )}
      </dl>

      {live && schedule && (
        <MoveForm
          booking={b}
          schedule={schedule}
          busy={busy}
          onMove={(courtId, start) =>
            void act(`/venue/bookings/${b.id}/move`, 'PUT', { courtId, start }, t('movedNotice'))
          }
          onUnchanged={() => setError(t('pickNewPlace'))}
        />
      )}

      {live &&
        (confirming ? (
          <div className="panel-section">
            <p>{t(isBlock ? 'confirmRemoveBlock' : 'confirmCancel')}</p>
            <div className="form-actions">
              <Button variant="secondary" onClick={() => setConfirming(false)}>
                {t('keepIt')}
              </Button>
              <Button
                variant="destructive"
                disabled={busy}
                onClick={() =>
                  void act(
                    `/venue/bookings/${b.id}/cancel`,
                    'POST',
                    undefined,
                    t(isBlock ? 'removedBlockNotice' : 'cancelledNotice'),
                  )
                }
              >
                {t(isBlock ? 'yesRemove' : 'yesCancel')}
              </Button>
            </div>
          </div>
        ) : (
          <div className="panel-section">
            <div>
              <Button variant="destructive" onClick={() => setConfirming(true)}>
                {t(isBlock ? 'removeBlock' : 'cancelBooking')}
              </Button>
            </div>
          </div>
        ))}

      {!live && b.status === 'Confirmed' && <p className="muted">{t('finishedNote')}</p>}
      {error && <Notice tone="error">{error}</Notice>}
      <div className="form-actions">
        <Button variant="secondary" onClick={onClose}>
          {t('close')}
        </Button>
      </div>
    </div>
  );
}

function MoveForm({
  booking: b,
  schedule,
  busy,
  onMove,
  onUnchanged,
}: {
  booking: StaffBooking;
  schedule: Schedule;
  busy: boolean;
  onMove: (courtId: string, start: string) => void;
  onUnchanged: () => void;
}) {
  const { t, name } = useI18n();
  const [courtId, setCourtId] = useState(b.courtId);
  const [start, setStart] = useState(b.startsAt);
  const sport = schedule.courts.find((c) => c.id === b.courtId)?.categoryId;
  // A booking stays on its sport; a block can go anywhere.
  const courts = schedule.courts.filter((c) => b.kind === 'Block' || c.categoryId === sport);
  const starts = slotStarts(schedule.from, schedule.to).filter((s) => minutesBetween(s, schedule.to) >= b.minutes);

  function submit(e: FormEvent) {
    e.preventDefault();
    if (courtId === b.courtId && start === b.startsAt) return onUnchanged();
    onMove(courtId, start);
  }

  return (
    <form className="panel-section" onSubmit={submit}>
      <div className="filter-bar">
        <Field label={t('moveTo')}>
          <select value={courtId} onChange={(e) => setCourtId(e.target.value)}>
            {courts.map((c) => (
              <option key={c.id} value={c.id}>
                {name(c)}
              </option>
            ))}
          </select>
        </Field>
        <Field label={t('startTime')}>
          <select value={start} onChange={(e) => setStart(e.target.value)}>
            {starts.map((s) => (
              <option key={s} value={s}>
                {clock(s)}
              </option>
            ))}
          </select>
        </Field>
        <Button type="submit" variant="secondary" disabled={busy}>
          {t('moveBooking')}
        </Button>
      </div>
    </form>
  );
}
```

- [ ] **Step 2: New booking / block dialog.** Create `admin/src/pages/schedule/NewBookingDialog.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '../../components/Button';
import { Dialog } from '../../components/Dialog';
import { Field } from '../../components/Field';
import { Notice } from '../../components/Notice';
import { api, ApiError, errorText } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import { clock, minutesBetween, slotStarts, STEP } from '../../lib/time';
import type { CustomerLookup, Schedule, StaffBooking } from '../../lib/types';

export type NewAt = { courtId: string; start: string };

const MAX_MINUTES = 720;

export function NewBookingDialog({
  schedule,
  at,
  onDone,
  onClose,
}: {
  schedule: Schedule;
  at: NewAt | undefined;
  onDone: (message: string) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  return (
    <Dialog open={at !== undefined} title={t('newBooking')} onClose={onClose}>
      {at && <NewBookingForm schedule={schedule} at={at} onDone={onDone} onClose={onClose} />}
    </Dialog>
  );
}

function NewBookingForm({
  schedule,
  at,
  onDone,
  onClose,
}: {
  schedule: Schedule;
  at: NewAt;
  onDone: (message: string) => void;
  onClose: () => void;
}) {
  const { t, name } = useI18n();
  const [mode, setMode] = useState<'booking' | 'block'>('booking');
  const [courtId, setCourtId] = useState(at.courtId);
  const [blockCourts, setBlockCourts] = useState<string[]>([at.courtId]);
  const [start, setStart] = useState(at.start);
  const court = schedule.courts.find((c) => c.id === courtId) ?? schedule.courts[0];
  const [picked, setPicked] = useState(court.durationsMinutes[0] ?? 60);
  const [customer, setCustomer] = useState('');
  const [phone, setPhone] = useState('');
  const [phoneHint, setPhoneHint] = useState<string>();
  const [note, setNote] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  // Staff may book any length in 30-minute steps that ends by closing.
  const left = Math.min(minutesBetween(start, schedule.to), MAX_MINUTES);
  const lengths = Array.from({ length: Math.floor(left / STEP) }, (_, k) => (k + 1) * STEP);
  const minutes = lengths.includes(picked) ? picked : lengths[lengths.length - 1];

  async function lookUp() {
    setPhoneHint(undefined);
    const number = phone.trim();
    if (!number) return;
    try {
      const found = await api<CustomerLookup>(`/venue/customers/lookup?phone=${encodeURIComponent(number)}`);
      if (found.name) {
        setPhoneHint(t('knownCustomer', { name: found.name }));
        setCustomer((current) => current || (found.name ?? ''));
      }
    } catch (err) {
      setPhoneHint(err instanceof ApiError && err.status === 404 ? t('newCustomer') : errorText(err));
    }
  }

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      if (mode === 'booking') {
        const created = await api<StaffBooking>('/venue/bookings', {
          method: 'POST',
          body: { courtId, start, minutes, name: customer, phone: phone.trim() || null, note: note.trim() || null },
        });
        onDone(t('bookedNotice', { ref: created.reference }));
      } else {
        await api<StaffBooking[]>('/venue/blocks', {
          method: 'POST',
          body: { courtIds: blockCourts, start, minutes, note: note.trim() || null },
        });
        onDone(t('blockedNotice'));
      }
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  const toggleCourt = (id: string) =>
    setBlockCourts((list) => (list.includes(id) ? list.filter((x) => x !== id) : [...list, id]));

  return (
    <form className="dialog-form" onSubmit={submit}>
      <div className="chip-row" role="radiogroup" aria-label={t('newBooking')}>
        {(['booking', 'block'] as const).map((m) => (
          <label key={m} className="check-chip">
            <input type="radio" name="mode" checked={mode === m} onChange={() => setMode(m)} />
            {t(m === 'booking' ? 'modeBooking' : 'modeBlock')}
          </label>
        ))}
      </div>

      <div className="form-grid">
        {mode === 'booking' ? (
          <Field label={t('court')}>
            <select value={courtId} onChange={(e) => setCourtId(e.target.value)}>
              {schedule.courts.map((c) => (
                <option key={c.id} value={c.id}>
                  {name(c)}
                </option>
              ))}
            </select>
          </Field>
        ) : (
          <fieldset className="mg-fieldset">
            <legend>{t('courts')}</legend>
            <div className="chip-row">
              {schedule.courts.map((c) => (
                <label key={c.id} className="check-chip">
                  <input type="checkbox" checked={blockCourts.includes(c.id)} onChange={() => toggleCourt(c.id)} />
                  {name(c)}
                </label>
              ))}
            </div>
          </fieldset>
        )}
        <Field label={t('startTime')}>
          <select value={start} onChange={(e) => setStart(e.target.value)}>
            {slotStarts(schedule.from, schedule.to).map((s) => (
              <option key={s} value={s}>
                {clock(s)}
              </option>
            ))}
          </select>
        </Field>
        <Field label={t('length')}>
          <select value={minutes} onChange={(e) => setPicked(Number(e.target.value))}>
            {lengths.map((m) => (
              <option key={m} value={m}>
                {t('minutesShort', { m })}
              </option>
            ))}
          </select>
        </Field>
      </div>

      {mode === 'booking' && (
        <div className="form-grid">
          <Field label={t('phoneOptional')} hint={phoneHint ?? t('phoneHint')}>
            <input
              type="tel"
              inputMode="tel"
              dir="ltr"
              autoComplete="off"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              onBlur={() => void lookUp()}
            />
          </Field>
          <Field label={t('customerName')}>
            <input required maxLength={60} value={customer} onChange={(e) => setCustomer(e.target.value)} />
          </Field>
        </div>
      )}

      <Field label={t('note')} hint={t('noteHint')}>
        <input maxLength={200} value={note} onChange={(e) => setNote(e.target.value)} />
      </Field>

      {error && <Notice tone="error">{error}</Notice>}
      <div className="form-actions">
        <Button variant="secondary" onClick={onClose}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t(mode === 'booking' ? 'book' : 'block')}
        </Button>
      </div>
    </form>
  );
}
```

- [ ] **Step 3: Schedule page.** Replace `admin/src/pages/schedule/SchedulePage.tsx`:

```tsx
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { Button } from '../../components/Button';
import { Notice } from '../../components/Notice';
import { PageHeader } from '../../components/PageHeader';
import { api, errorText } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import { addDays, clock, formatDay, minutesBetween, slotStarts, STEP } from '../../lib/time';
import type { Schedule } from '../../lib/types';
import { useLoad } from '../../lib/useLoad';
import { useToast } from '../../lib/useToast';
import { BookingPanel } from './BookingPanel';
import { NewBookingDialog, type NewAt } from './NewBookingDialog';

/** ?date=YYYY-MM-DD; no date = the venue's current business day. */
export function SchedulePage() {
  const [params, setParams] = useSearchParams();
  const date = params.get('date') ?? '';
  return <ScheduleDay key={date} date={date} onDate={(d) => setParams(d ? { date: d } : {})} />;
}

function ScheduleDay({ date, onDate }: { date: string; onDate: (date: string) => void }) {
  const { t, lang, name } = useI18n();
  const schedule = useLoad(() => api<Schedule>(`/venue/schedule${date ? `?date=${date}` : ''}`));
  const toast = useToast();
  const [newAt, setNewAt] = useState<NewAt>();
  const [openId, setOpenId] = useState<string>();
  const [dropAt, setDropAt] = useState<string>();

  function done(message: string) {
    setNewAt(undefined);
    setOpenId(undefined);
    toast.show('success', message);
    schedule.reload();
  }

  async function move(id: string, courtId: string, start: string) {
    const b = schedule.data?.bookings.find((x) => x.id === id);
    if (!b || (b.courtId === courtId && b.startsAt === start)) return;
    try {
      await api(`/venue/bookings/${id}/move`, { method: 'PUT', body: { courtId, start } });
      done(t('movedNotice'));
    } catch (err) {
      toast.show('error', errorText(err));
    }
  }

  if (schedule.error) {
    return (
      <div className="page">
        <Notice tone="error">{schedule.error}</Notice>
      </div>
    );
  }
  if (!schedule.data) return <p className="page-loading">{t('loading')}</p>;
  const s = schedule.data;
  const starts = slotStarts(s.from, s.to);
  const row = (iso: string) => Math.round(minutesBetween(s.from, iso) / STEP);
  const nowMs = Date.now();
  const firstFuture = starts.find((x) => new Date(x).getTime() >= nowMs) ?? starts[0];
  const canBook = s.open && s.courts.length > 0 && starts.length > 0;

  return (
    <div className="page page--wide">
      <PageHeader
        title={t('navSchedule')}
        action={
          <div className="day-nav">
            <Button variant="secondary" aria-label={t('previousDay')} onClick={() => onDate(addDays(s.date, -1))}>
              <ChevronLeft className="flip-rtl" size={18} aria-hidden="true" />
            </Button>
            <input type="date" aria-label={t('date')} value={s.date} onChange={(e) => e.target.value && onDate(e.target.value)} />
            <Button variant="secondary" aria-label={t('nextDay')} onClick={() => onDate(addDays(s.date, 1))}>
              <ChevronRight className="flip-rtl" size={18} aria-hidden="true" />
            </Button>
            <Button variant="text" onClick={() => onDate('')}>
              {t('navToday')}
            </Button>
            {canBook && (
              <Button onClick={() => setNewAt({ courtId: s.courts[0].id, start: firstFuture })}>{t('newBooking')}</Button>
            )}
          </div>
        }
      />
      <p className="page-intro">
        {formatDay(s.date, lang)}
        {canBook && ` · ${t('scheduleHint')}`}
      </p>

      {s.courts.length === 0 ? (
        <p className="empty">{t('scheduleNoCourts')}</p>
      ) : !s.open ? (
        <Notice tone="info">{t('closedOnDay')}</Notice>
      ) : (
        <div className="schedule-scroll">
          <div
            className="schedule-grid"
            style={{
              gridTemplateColumns: `64px repeat(${s.courts.length}, minmax(150px, 1fr))`,
              gridTemplateRows: `40px repeat(${starts.length}, 28px)`,
            }}
          >
            <div className="schedule-corner" style={{ gridColumn: 1, gridRow: 1 }} />
            {s.courts.map((c, i) => (
              <div key={c.id} className="schedule-court" style={{ gridColumn: i + 2, gridRow: 1 }}>
                {name(c)}
              </div>
            ))}
            {starts.map((start, k) => (
              <div key={start} className="schedule-time" style={{ gridColumn: 1, gridRow: k + 2 }}>
                {k % 2 === 0 ? clock(start) : ''}
              </div>
            ))}
            {s.courts.flatMap((c, i) =>
              starts.map((start, k) => {
                const key = `${c.id}|${start}`;
                return (
                  <button
                    key={key}
                    type="button"
                    tabIndex={-1}
                    className={['schedule-cell', k % 2 === 0 && 'on-hour', dropAt === key && 'drop-target'].filter(Boolean).join(' ')}
                    style={{ gridColumn: i + 2, gridRow: k + 2 }}
                    aria-label={t('bookCourtAt', { court: name(c), time: clock(start) })}
                    onClick={() => setNewAt({ courtId: c.id, start })}
                    onDragOver={(e) => {
                      e.preventDefault();
                      setDropAt(key);
                    }}
                    onDragLeave={() => setDropAt(undefined)}
                    onDrop={(e) => {
                      e.preventDefault();
                      setDropAt(undefined);
                      void move(e.dataTransfer.getData('text/plain'), c.id, start);
                    }}
                  />
                );
              }),
            )}
            {s.bookings.map((b) => {
              const column = s.courts.findIndex((c) => c.id === b.courtId);
              if (column < 0) return null;
              const top = Math.max(0, row(b.startsAt));
              const bottom = Math.min(starts.length, row(b.endsAt));
              const live = new Date(b.endsAt).getTime() > nowMs;
              return (
                <button
                  key={b.id}
                  type="button"
                  draggable={live}
                  className={`schedule-booking schedule-booking--${b.kind.toLowerCase()}`}
                  style={{ gridColumn: column + 2, gridRow: `${top + 2} / ${bottom + 2}` }}
                  onClick={() => setOpenId(b.id)}
                  onDragStart={(e) => {
                    e.dataTransfer.setData('text/plain', b.id);
                    e.dataTransfer.effectAllowed = 'move';
                  }}
                >
                  <span className="schedule-booking-time">
                    {clock(b.startsAt)}–{clock(b.endsAt)}
                  </span>
                  <span>{b.kind === 'Block' ? (b.note ?? t('blocked')) : b.customerName}</span>
                </button>
              );
            })}
          </div>
        </div>
      )}

      <NewBookingDialog schedule={s} at={newAt} onDone={done} onClose={() => setNewAt(undefined)} />
      <BookingPanel
        booking={s.bookings.find((b) => b.id === openId)}
        schedule={s}
        onDone={done}
        onClose={() => setOpenId(undefined)}
      />
      {toast.view}
    </div>
  );
}
```

- [ ] **Step 4:** `npm run build`, `npm run lint` → clean. **Commit** `feat(admin): schedule grid with click-to-book, block time, drag to move and a booking panel`.

---

### Task 4: Bookings and Customers pages

- [ ] **Step 1: Bookings.** Replace `admin/src/pages/BookingsPage.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { BookingStatusPill } from '../components/BookingStatusPill';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { addDays, clock, dayOf, formatDay } from '../lib/time';
import type { StaffBooking, Today } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import type { MessageKey } from '../locales/en';
import { BookingPanel } from './schedule/BookingPanel';

type Filter = 'today' | 'week' | 'cancelled' | 'dates';
const FILTERS: [Filter, MessageKey][] = [
  ['today', 'navToday'],
  ['week', 'filterWeek'],
  ['cancelled', 'tileCancelled'],
  ['dates', 'filterDates'],
];

export function BookingsPage() {
  const { t } = useI18n();
  // The venue's business day, so "Today" means the same thing as on the Today page.
  const today = useLoad(() => api<Today>('/venue/today'));
  if (today.error) {
    return (
      <div className="page">
        <Notice tone="error">{today.error}</Notice>
      </div>
    );
  }
  if (!today.data) return <p className="page-loading">{t('loading')}</p>;
  return <BookingsView today={today.data.date} />;
}

function BookingsView({ today }: { today: string }) {
  const { t } = useI18n();
  const [filter, setFilter] = useState<Filter>('today');
  const [from, setFrom] = useState(today);
  const [to, setTo] = useState(addDays(today, 6));
  const [term, setTerm] = useState('');
  const [q, setQ] = useState('');

  const range: [string, string] =
    filter === 'today'
      ? [today, today]
      : filter === 'week'
        ? [today, addDays(today, 6)]
        : filter === 'cancelled'
          ? [addDays(today, -30), addDays(today, 30)]
          : [from, to];
  const query = new URLSearchParams({
    from: range[0],
    to: range[1],
    ...(filter === 'cancelled' ? { status: 'Cancelled' } : {}),
    ...(q ? { q } : {}),
  }).toString();

  function search(e: FormEvent) {
    e.preventDefault();
    setQ(term.trim());
  }

  return (
    <div className="page">
      <PageHeader title={t('navBookings')} />
      <div className="filter-bar">
        <div className="chip-row" role="radiogroup" aria-label={t('navBookings')}>
          {FILTERS.map(([value, label]) => (
            <label key={value} className="check-chip">
              <input type="radio" name="filter" checked={filter === value} onChange={() => setFilter(value)} />
              {t(label)}
            </label>
          ))}
        </div>
        {filter === 'dates' && (
          <>
            <Field label={t('fromTime')}>
              <input type="date" required value={from} onChange={(e) => e.target.value && setFrom(e.target.value)} />
            </Field>
            <Field label={t('toTime')}>
              <input type="date" required value={to} onChange={(e) => e.target.value && setTo(e.target.value)} />
            </Field>
          </>
        )}
      </div>
      <form className="search-form" onSubmit={search} role="search">
        <Field label={t('search')} hint={t('searchBookingsHint')}>
          <input type="search" value={term} onChange={(e) => setTerm(e.target.value)} />
        </Field>
        <Button type="submit" variant="secondary">
          {t('search')}
        </Button>
      </form>
      <BookingsTable key={query} query={query} />
    </div>
  );
}

function BookingsTable({ query }: { query: string }) {
  const { t, lang, name, money } = useI18n();
  const list = useLoad(() => api<StaffBooking[]>(`/venue/bookings?${query}`));
  const toast = useToast();
  const [openId, setOpenId] = useState<string>();

  if (list.error) return <Notice tone="error">{list.error}</Notice>;
  if (!list.data) return <p className="page-loading">{t('loading')}</p>;
  const rows = list.data;

  return (
    <>
      {rows.length === 0 ? (
        <p className="empty">{t('noBookings')}</p>
      ) : (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('reference')}</th>
              <th>{t('time')}</th>
              <th>{t('court')}</th>
              <th>{t('customer')}</th>
              <th className="numeric">{t('price')}</th>
              <th>{t('status')}</th>
              <th>{t('source')}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((b) => (
              <tr key={b.id}>
                <td>
                  <Button variant="text" onClick={() => setOpenId(b.id)}>
                    {b.reference}
                  </Button>
                </td>
                <td>
                  {formatDay(dayOf(b.startsAt), lang, 'short')} · {clock(b.startsAt)}–{clock(b.endsAt)}
                </td>
                <td>{name({ nameEn: b.courtNameEn, nameAr: b.courtNameAr })}</td>
                <td>
                  {b.customerName}{' '}
                  <bdi dir="ltr" className="muted">
                    {b.phoneMasked ?? t('walkIn')}
                  </bdi>
                </td>
                <td className="numeric">{money(b.total, b.currency)}</td>
                <td>
                  <BookingStatusPill booking={b} />
                </td>
                <td>{t(b.source === 'App' ? 'sourceApp' : 'sourceStaff')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <BookingPanel
        booking={rows.find((b) => b.id === openId)}
        onDone={(message) => {
          setOpenId(undefined);
          toast.show('success', message);
          list.reload();
        }}
        onClose={() => setOpenId(undefined)}
      />
      {toast.view}
    </>
  );
}
```

- [ ] **Step 2: Customers.** Replace `admin/src/pages/CustomersPage.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { clock, dayOf, formatDay } from '../lib/time';
import type { VenueCustomer } from '../lib/types';
import { useLoad } from '../lib/useLoad';

export function CustomersPage() {
  const { t } = useI18n();
  const [term, setTerm] = useState('');
  const [q, setQ] = useState('');

  function search(e: FormEvent) {
    e.preventDefault();
    setQ(term.trim());
  }

  return (
    <div className="page">
      <PageHeader title={t('navCustomers')} />
      <p className="page-intro">{t('customersIntro')}</p>
      <form className="search-form" onSubmit={search} role="search">
        <Field label={t('search')} hint={t('searchCustomersHint')}>
          <input type="search" value={term} onChange={(e) => setTerm(e.target.value)} />
        </Field>
        <Button type="submit" variant="secondary">
          {t('search')}
        </Button>
      </form>
      <CustomersTable key={q} q={q} />
    </div>
  );
}

function CustomersTable({ q }: { q: string }) {
  const { t, lang } = useI18n();
  const list = useLoad(() => api<VenueCustomer[]>(`/venue/customers${q ? `?q=${encodeURIComponent(q)}` : ''}`));

  if (list.error) return <Notice tone="error">{list.error}</Notice>;
  if (!list.data) return <p className="page-loading">{t('loading')}</p>;
  if (list.data.length === 0) return <p className="empty">{t('noCustomers')}</p>;

  return (
    <table className="mg-table">
      <thead>
        <tr>
          <th>{t('customer')}</th>
          <th>{t('phone')}</th>
          <th className="numeric">{t('navBookings')}</th>
          <th className="numeric">{t('cancellations')}</th>
          <th>{t('lastBooking')}</th>
        </tr>
      </thead>
      <tbody>
        {list.data.map((c) => (
          <tr key={c.id}>
            <td>{c.name ?? <span className="muted">{t('noName')}</span>}</td>
            <td>
              <bdi dir="ltr">{c.phoneMasked}</bdi>
            </td>
            <td className="numeric">{c.bookings}</td>
            <td className="numeric">{c.cancellations}</td>
            <td>
              {formatDay(dayOf(c.lastBookingAt), lang, 'short')} · {clock(c.lastBookingAt)}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
```

- [ ] **Step 3:** `npm run build`, `npm run lint` → clean. **Commit** `feat(admin): bookings list with quick filters and search; customers with masked phones`.

---

### Task 5: Browser walkthrough (controller)

Run the API (:5300) and `npm run dev` (:5173). Seed a venue (16:00–02:00, two padel courts, one football pitch, published) with a manager and a desk user through the API, as in the M6a script. In Chrome (chrome-devtools MCP), at desktop width, then 390 px, in English then Arabic:

1. Desk signs in (forced password change) and lands on **Today**: date, opening hours, four tiles, "Nothing else booked today".
2. **Schedule**: grid 16:00 → 02:00 with sticky court headers and time column. Click 20:00 on Court 1 → dialog prefilled; type a new phone → "New customer" hint; name, 120 min, Book → toast with reference, booking appears 20:00–22:00.
3. Click another free cell → "Block time", two courts, 60 min, note → hatched blocks on both.
4. Drag a booking onto another court/time → "Booking moved". Drag onto a taken time → error toast, nothing moves. Open a booking → panel; Move form to a new start; Cancel with the two-step confirm.
5. Phone lookup on a second booking with the same number → "Booked here before as …", name prefilled.
6. Previous/next day and the date input change the day; "Today" returns.
7. **Bookings**: Today / Next 7 days / Cancelled / Dates; search by last 4 digits, by reference, by name; open a row → panel (cancel only).
8. **Customers**: masked phones only; search by name and by digits.
9. **Venue**: manager sets free cancellation to 12 h, saves, reloads → 12; desk sees it read-only.
10. **Arabic**: RTL grid (courts flow right-to-left, arrows flip), Gregorian dates with Western digits, masked phone renders "•••• 1234" left-to-right.
11. 390 px: grid scrolls horizontally inside its frame; no page-wide horizontal scroll.

Fix what fails, re-check, tick M6b in the roadmap, commit, merge.

## What M6b deliberately leaves out

- A "now" line across the grid and live auto-refresh of the schedule (Today refreshes; the grid reloads after each action).
- Resizing a booking by dragging its edge (length changes = cancel and rebook for now).
- Price preview in the new-booking dialog (the booking toast and panel show the price).
- Customer detail page / booking history per customer.

## Walkthrough fixes (Task 5)

- Time ranges reversed in Arabic (`17:00–16:00`): every range is now wrapped in `<bdi dir="ltr">`.
- Customers `lastBookingAt` came back in UTC; the API now converts it to the venue's offset.
- A refused move said the customer-app sentence; staff now get "That court already has a booking in that time. Nothing was moved."
- `.mg-button` on a router `<Link>` was underlined and misaligned; buttons are now inline-flex, centred, no underline.

