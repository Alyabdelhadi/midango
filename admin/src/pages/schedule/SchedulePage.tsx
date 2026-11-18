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
  // eslint-disable-next-line react/purity -- re-read on every render (each action reloads the day)
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
