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
