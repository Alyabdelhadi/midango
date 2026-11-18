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
                  {formatDay(dayOf(b.startsAt), lang, 'short')} · <bdi dir="ltr">{clock(b.startsAt)}–{clock(b.endsAt)}</bdi>
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
