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
  // eslint-disable-next-line react/purity -- the panel mounts fresh on each open; "now" at open is what we want
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
          {formatDay(dayOf(b.startsAt), lang)} · <bdi dir="ltr">{clock(b.startsAt)}–{clock(b.endsAt)}</bdi> ({t('minutesShort', { m: b.minutes })})
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
