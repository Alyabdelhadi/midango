import { useState } from 'react';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { recent } from '../lib/time';
import type { StaffBooking, StaffMatchPlayer } from '../lib/types';
import { Button } from './Button';
import { Pill } from './Pill';

const HOUR = 3_600_000;

/** The API's desk windows: check-in from an hour before the start to six hours after the end; no-show after the start. */
function deskWindow(b: StaffBooking, now: number) {
  const start = new Date(b.startsAt).getTime();
  const end = new Date(b.endsAt).getTime();
  return { canCheckIn: now >= start - HOUR && now <= end + 6 * HOUR, canNoShow: now >= start };
}

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
  const { canCheckIn, canNoShow } = deskWindow(b, Date.now());
  if (!canCheckIn && !canNoShow) return null;

  // An open match's organiser pays only their part; each player pays a share at the counter.
  const amount = money(b.match ? b.match.organiserShare : b.total, b.currency);
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

/** An open match's players under the organiser: who's in, the cash taken so far, and per-player
 *  check-in / no-show / undo on the same rules as the booking itself. */
export function MatchPlayers({ booking: b, onDone }: { booking: StaffBooking; onDone: (message: string, undo?: Undo) => void }) {
  const { t, money } = useI18n();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const m = b.match;
  if (!m) return null;

  const { canCheckIn, canNoShow } =
    // eslint-disable-next-line react/purity -- the buttons follow the clock; the page re-renders on reload
    b.status === 'Confirmed' ? deskWindow(b, Date.now()) : { canCheckIn: false, canNoShow: false };
  const taken = m.players.reduce((sum, p) => sum + (p.cashCollected ?? 0), b.cashCollected ?? 0);
  const share = money(m.share, b.currency);
  const url = (p: StaffMatchPlayer, action: string) => `/venue/bookings/${b.id}/players/${p.customerId}/${action}`;

  async function run(p: StaffMatchPlayer, action: string, undoAction: string | null, message: string) {
    setBusy(true);
    setError(undefined);
    try {
      await api(url(p, action), { method: 'POST' });
      onDone(message, undoAction ? () => api(url(p, undoAction), { method: 'POST' }) : undefined);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="match-players">
      <p className="match-players-head">
        <strong>{t('matchPlayers')}</strong>
        <span>{t('matchCollected', { taken: money(taken, b.currency), total: money(b.total, b.currency) })}</span>
      </p>
      {m.players.length === 0 ? (
        <p className="muted">{t('noPlayersYet')}</p>
      ) : (
        <ul className="match-player-list">
          {m.players.map((p) => {
            const who = p.name ?? p.maskedPhone;
            const waiting = !p.checkedInAt && !p.noShowAt;
            return (
              <li key={p.customerId} className="match-player">
                <span className="match-player-who">
                  {p.name}{' '}
                  <bdi dir="ltr" className="muted">
                    {p.maskedPhone}
                  </bdi>
                </span>
                {p.noShowAt ? (
                  <Pill tone="warning">{t('statusNoShow')}</Pill>
                ) : p.checkedInAt ? (
                  <Pill tone="success">{t('statusCheckedInAmount', { amount: money(p.cashCollected ?? m.share, b.currency) })}</Pill>
                ) : (
                  <Pill>{t('statusWaiting')}</Pill>
                )}
                <span className="desk-actions">
                  {waiting && canCheckIn && (
                    <Button
                      disabled={busy}
                      aria-label={t('takeCashCheckInFor', { amount: share, name: who })}
                      onClick={() => void run(p, 'check-in', 'undo-check-in', t('checkedInNotice', { name: who, amount: share }))}
                    >
                      {t('takeCashCheckIn', { amount: share })}
                    </Button>
                  )}
                  {waiting && canNoShow && (
                    <Button
                      variant="secondary"
                      disabled={busy}
                      aria-label={t('markNoShowFor', { name: who })}
                      onClick={() => void run(p, 'no-show', 'undo-no-show', t('playerNoShowNotice', { name: who }))}
                    >
                      {t('markNoShow')}
                    </Button>
                  )}
                  {recent(p.checkedInAt) && (
                    <Button
                      variant="secondary"
                      disabled={busy}
                      aria-label={t('undoCheckInFor', { name: who })}
                      onClick={() => void run(p, 'undo-check-in', null, t('undone'))}
                    >
                      {t('undoCheckIn')}
                    </Button>
                  )}
                  {recent(p.noShowAt) && (
                    <Button
                      variant="secondary"
                      disabled={busy}
                      aria-label={t('undoNoShowFor', { name: who })}
                      onClick={() => void run(p, 'undo-no-show', null, t('undone'))}
                    >
                      {t('undoNoShow')}
                    </Button>
                  )}
                </span>
              </li>
            );
          })}
        </ul>
      )}
      {error && (
        <span className="desk-error" role="alert">
          {error}
        </span>
      )}
    </div>
  );
}
