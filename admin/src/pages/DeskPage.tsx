import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { BookingStatusPill } from '../components/BookingStatusPill';
import { Button } from '../components/Button';
import { DeskActions, MatchPlayers, type Undo } from '../components/DeskActions';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { clock, dayOf, minutesBetween, slotStarts } from '../lib/time';
import type { Schedule, StaffBooking, Today, Tournament, TournamentSummary } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import { BookingPanel } from './schedule/BookingPanel';
import { NewBookingDialog, type NewAt } from './schedule/NewBookingDialog';

/** The counter: today's bookings as Now / Next / Earlier, a reference box for typed or scanned codes,
 *  one-tap check-in, no-show, walk-ins and the cash taken so far. Refreshes every 30 s. */
export function DeskPage() {
  const { t, money } = useI18n();
  const today = useLoad(() => api<Today>('/venue/today'));
  const toast = useToast();
  const [openBooking, setOpenBooking] = useState<StaffBooking>();
  const [walkIn, setWalkIn] = useState<{ schedule: Schedule; at: NewAt }>();
  const [reference, setReference] = useState('');
  const [lookupError, setLookupError] = useState<string>();
  const [version, setVersion] = useState(0);
  const reloadToday = today.reload;

  useEffect(() => {
    const timer = setInterval(() => {
      reloadToday();
      setVersion((v) => v + 1);
    }, 30_000);
    return () => clearInterval(timer);
  }, [reloadToday]);

  function reloadAll() {
    reloadToday();
    setVersion((v) => v + 1);
  }

  function done(message: string, undo?: Undo) {
    setOpenBooking(undefined);
    setWalkIn(undefined);
    toast.show('success', message, undo && (async () => { await undo(); reloadAll(); }));
    reloadAll();
  }

  async function lookUp(e: FormEvent) {
    e.preventDefault();
    setLookupError(undefined);
    const code = reference.trim();
    if (!code) return;
    try {
      setOpenBooking(await api<StaffBooking>(`/venue/bookings/lookup?reference=${encodeURIComponent(code)}`));
      setReference('');
    } catch (err) {
      setLookupError(errorText(err));
    }
  }

  async function startWalkIn() {
    try {
      const schedule = await api<Schedule>('/venue/schedule');
      const starts = slotStarts(schedule.from, schedule.to);
      // The half hour we're in now, on the first court that's free for it.
      const now = Date.now();
      const start = [...starts].reverse().find((s) => new Date(s).getTime() <= now) ?? starts[0];
      if (!schedule.open || !start || schedule.courts.length === 0) return toast.show('error', t('closedToday'));
      const busy = (courtId: string) =>
        schedule.bookings.some(
          (b) => b.courtId === courtId && minutesBetween(b.startsAt, start) < b.minutes && minutesBetween(start, b.startsAt) < 30,
        );
      const court = schedule.courts.find((c) => !busy(c.id)) ?? schedule.courts[0];
      setWalkIn({ schedule, at: { courtId: court.id, start } });
    } catch (err) {
      toast.show('error', errorText(err));
    }
  }

  if (today.error) {
    return (
      <div className="page">
        <Notice tone="error">{today.error}</Notice>
      </div>
    );
  }
  if (!today.data) return <p className="page-loading">{t('loading')}</p>;
  const d = today.data;

  return (
    <div className="page">
      <PageHeader title={t('navDesk')} action={<Button onClick={() => void startWalkIn()}>{t('newWalkIn')}</Button>} />
      <form className="search-form" onSubmit={lookUp} role="search">
        <Field label={t('lookupLabel')} hint={t('lookupHint')}>
          <input
            autoFocus
            autoComplete="off"
            spellCheck={false}
            dir="ltr"
            maxLength={12}
            value={reference}
            onChange={(e) => setReference(e.target.value)}
          />
        </Field>
        <Button type="submit" variant="secondary">
          {t('search')}
        </Button>
      </form>
      {lookupError && <Notice tone="error">{lookupError}</Notice>}
      <p className="desk-summary">
        {t('cashSummary', { taken: money(d.cashCollected, d.currency), expected: money(d.cashExpected, d.currency) })}
      </p>
      <TournamentsToday key={`t|${d.date}|${version}`} date={d.date} />
      <DeskLists key={`${d.date}|${version}`} date={d.date} onOpen={setOpenBooking} onDone={done} />

      <BookingPanel booking={openBooking} onDone={done} onClose={() => setOpenBooking(undefined)} />
      {walkIn && (
        <NewBookingDialog schedule={walkIn.schedule} at={walkIn.at} onDone={done} onClose={() => setWalkIn(undefined)} />
      )}
      {toast.view}
    </div>
  );
}

function DeskLists({
  date,
  onOpen,
  onDone,
}: {
  date: string;
  onOpen: (b: StaffBooking) => void;
  onDone: (message: string, undo?: Undo) => void;
}) {
  const { t } = useI18n();
  const list = useLoad(() => api<StaffBooking[]>(`/venue/bookings?from=${date}`));
  if (list.error) return <Notice tone="error">{list.error}</Notice>;
  if (!list.data) return <p className="page-loading">{t('loading')}</p>;

  // eslint-disable-next-line react/purity -- grouping follows the clock; the page reloads every 30 s
  const now = Date.now();
  const rows = list.data.filter((b) => b.status !== 'Cancelled');
  const nowRows = rows.filter((b) => new Date(b.startsAt).getTime() <= now && new Date(b.endsAt).getTime() > now);
  const nextRows = rows.filter((b) => new Date(b.startsAt).getTime() > now);
  const earlierRows = rows.filter((b) => new Date(b.endsAt).getTime() <= now);

  const listOf = (items: StaffBooking[]) => (
    <ul className="desk-list">
      {items.map((b) => (
        <DeskRow key={b.id} booking={b} onOpen={onOpen} onDone={onDone} />
      ))}
    </ul>
  );

  return (
    <>
      <section className="card">
        <h2>{t('sectionNow')}</h2>
        {nowRows.length === 0 ? <p className="empty">{t('nothingNow')}</p> : listOf(nowRows)}
      </section>
      <section className="card">
        <h2>{t('sectionNext')}</h2>
        {nextRows.length === 0 ? <p className="empty">{t('nothingNext')}</p> : listOf(nextRows)}
      </section>
      {earlierRows.length > 0 && (
        <details className="card desk-earlier">
          <summary>{t('sectionEarlier', { n: earlierRows.length })}</summary>
          {listOf(earlierRows)}
        </details>
      )}
    </>
  );
}

function DeskRow({
  booking: b,
  onOpen,
  onDone,
}: {
  booking: StaffBooking;
  onOpen: (b: StaffBooking) => void;
  onDone: (message: string, undo?: Undo) => void;
}) {
  const { t, name, money } = useI18n();
  return (
    <li className="desk-row">
      <span className="desk-row-time">
        <bdi dir="ltr">
          {clock(b.startsAt)}–{clock(b.endsAt)}
        </bdi>
      </span>
      <span className="desk-row-who">
        <Button variant="text" onClick={() => onOpen(b)}>
          {b.customerName}
        </Button>
        <span className="desk-row-meta">
          <span>{name({ nameEn: b.courtNameEn, nameAr: b.courtNameAr })}</span>
          <bdi dir="ltr">{b.phoneMasked ?? t('walkIn')}</bdi>
          <span>{money(b.total, b.currency)}</span>
          {b.pointsDiscount > 0 && <span>{t('withPoints', { amount: money(b.pointsDiscount, b.currency) })}</span>}
          <span>{b.reference}</span>
          <BookingStatusPill booking={b} />
          {b.match && <Pill>{t('openMatchCount', { n: b.match.players.length + 1, max: b.match.maxPlayers })}</Pill>}
        </span>
      </span>
      <DeskActions booking={b} onDone={onDone} />
      {b.match && (
        <div className="desk-row-match">
          <MatchPlayers booking={b} onDone={onDone} />
        </div>
      )}
    </li>
  );
}

/** A line per published tournament starting today, linking to its check-in page. */
function TournamentsToday({ date }: { date: string }) {
  const list = useLoad(() => api<TournamentSummary[]>('/venue/tournaments'));
  const today = list.data?.filter((x) => x.status === 'Published' && dayOf(x.startsAt) === date) ?? [];
  return today.map((x) => <TournamentTodayLine key={x.id} id={x.id} />);
}

function TournamentTodayLine({ id }: { id: string }) {
  const { t, name } = useI18n();
  // The list has no check-in counts; the detail does.
  const detail = useLoad(() => api<Tournament>(`/venue/tournaments/${id}`));
  if (!detail.data) return null;
  const registered = detail.data.teams.filter((x) => x.status === 'Registered');
  return (
    <Notice tone="info">
      <Link to={`/tournaments/${id}`}>
        {t('tournamentToday', {
          name: name(detail.data),
          checked: registered.filter((x) => x.checkedInAt).length,
          registered: registered.length,
        })}
      </Link>
    </Notice>
  );
}
