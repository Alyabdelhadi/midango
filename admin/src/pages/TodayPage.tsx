import { useEffect } from 'react';
import { Link } from 'react-router';
import { BookingStatusPill } from '../components/BookingStatusPill';
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
  // Wall-clock read per render on purpose: the 60 s reload re-renders and moves "Playing now".
  // eslint-disable-next-line react/purity
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
        {d.open && (
          <>
            {' · '}
            <bdi dir="ltr">
              {clock(d.from)}–{clock(d.to)}
            </bdi>
          </>
        )}
      </p>
      {!d.open && <Notice tone="info">{t('closedToday')}</Notice>}
      <div className="tiles">
        <Tile label={t('tileBookings')} value={String(d.bookings)} />
        <Tile label={t('tileOccupancy')} value={`${d.occupancyPercent}%`} />
        <Tile
          label={t('tileCashTaken')}
          value={money(d.cashCollected, d.currency)}
          note={t('ofExpected', { amount: money(d.cashExpected, d.currency) })}
        />
        <Tile label={t('tileNoShows')} value={String(d.noShows)} />
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
                  <bdi dir="ltr">{clock(b.startsAt)}–{clock(b.endsAt)}</bdi>
                </span>
                <span>{name({ nameEn: b.courtNameEn, nameAr: b.courtNameAr })}</span>
                <span>
                  {b.customerName}{' '}
                  <bdi dir="ltr" className="muted">
                    {b.phoneMasked ?? t('walkIn')}
                  </bdi>
                </span>
                {new Date(b.startsAt).getTime() <= now && <Pill tone="success">{t('playingNow')}</Pill>}
                {b.checkedInAt && <BookingStatusPill booking={b} />}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function Tile({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="tile">
      <span className="tile-label">{label}</span>
      <span className="tile-value">{value}</span>
      {note && <span className="tile-note">{note}</span>}
    </div>
  );
}
