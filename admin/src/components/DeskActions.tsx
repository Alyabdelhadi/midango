import { useState } from 'react';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { StaffBooking } from '../lib/types';
import { Button } from './Button';

const HOUR = 3_600_000;

/** Undo for the action just taken; the page reloads its data after it. */
export type Undo = () => Promise<unknown>;

/** The counter's next step for a booking, from its state: take cash & check in, and/or no-show.
 *  Renders nothing when neither applies (block, cancelled, checked in, no-show, outside the window). */
export function DeskActions({ booking: b, onDone }: { booking: StaffBooking; onDone: (message: string, undo: Undo) => void }) {
  const { t, money } = useI18n();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  if (b.kind === 'Block' || b.status !== 'Confirmed' || b.checkedInAt) return null;

  // eslint-disable-next-line react/purity -- the buttons follow the clock; the page re-renders on reload
  const now = Date.now();
  const start = new Date(b.startsAt).getTime();
  const end = new Date(b.endsAt).getTime();
  const canCheckIn = now >= start - HOUR && now <= end + 6 * HOUR;
  const canNoShow = now >= start;
  if (!canCheckIn && !canNoShow) return null;

  const amount = money(b.total, b.currency);
  const who = b.customerName ?? '';

  async function run(action: string, undoAction: string, message: string) {
    setBusy(true);
    setError(undefined);
    try {
      await api(`/venue/bookings/${b.id}/${action}`, { method: 'POST' });
      onDone(message, () => api(`/venue/bookings/${b.id}/${undoAction}`, { method: 'POST' }));
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="desk-actions">
      {canCheckIn && (
        <Button disabled={busy} onClick={() => void run('check-in', 'undo-check-in', t('checkedInNotice', { name: who, amount }))}>
          {t('takeCashCheckIn', { amount })}
        </Button>
      )}
      {canNoShow && (
        <Button variant="secondary" disabled={busy} onClick={() => void run('no-show', 'undo-no-show', t('noShowNotice', { name: who }))}>
          {t('markNoShow')}
        </Button>
      )}
      {error && (
        <span className="desk-error" role="alert">
          {error}
        </span>
      )}
    </div>
  );
}
