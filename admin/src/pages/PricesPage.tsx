import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { DAY_KEY, DAYS, type Band, type Category, type Day, type Quote, type Venue } from '../lib/types';
import { useLoad } from '../lib/useLoad';

/** One editable row: the same period and price on several days. */
type Row = { key: string; days: Day[]; from: string; to: string; price: string };

function rowsFromBands(bands: Band[]): Row[] {
  const rows = new Map<string, Row>();
  for (const band of bands) {
    const id = `${band.from}|${band.to}|${band.hourlyPrice}`;
    const row = rows.get(id) ?? {
      key: crypto.randomUUID(),
      days: [],
      from: band.from.slice(0, 5),
      to: band.to.slice(0, 5),
      price: String(band.hourlyPrice),
    };
    row.days.push(band.day);
    rows.set(id, row);
  }
  return [...rows.values()].map((row) => ({ ...row, days: DAYS.filter((d) => row.days.includes(d)) }));
}

function bandsFromRows(rows: Row[]): Band[] {
  return rows.flatMap((row) =>
    row.days.map((day) => ({ day, from: `${row.from}:00`, to: `${row.to}:00`, hourlyPrice: Number(row.price) })),
  );
}

const boardKey = (bands: Band[]) =>
  bands.map((b) => `${b.day}|${b.from}|${b.to}|${b.hourlyPrice}`).sort().join(',');

export function PricesPage({ canEdit }: { canEdit: boolean }) {
  const { t } = useI18n();
  const venue = useLoad(() => api<Venue>('/venue'));
  const categories = useLoad(() => api<Category[]>('/categories'));

  if (venue.error) {
    return (
      <div className="page">
        <Notice tone="error">{venue.error}</Notice>
      </div>
    );
  }
  if (!venue.data || !categories.data) return <p className="page-loading">{t('loading')}</p>;

  return (
    <div className="page">
      <PageHeader title={t('navPrices')} />
      <p className="page-intro">{t('pricesIntro')}</p>
      {venue.data.courts.length === 0 ? (
        <p className="empty">{t('noCourts')}</p>
      ) : (
        <>
          <PriceBoard venue={venue.data} canEdit={canEdit} onSaved={venue.setData} />
          <PriceCheck venue={venue.data} categories={categories.data} />
        </>
      )}
    </div>
  );
}

function PriceBoard({ venue, canEdit, onSaved }: { venue: Venue; canEdit: boolean; onSaved: (venue: Venue) => void }) {
  const { t, name, money } = useI18n();
  const [selected, setSelected] = useState<string[]>(() => venue.courts.map((c) => c.id));
  const [rows, setRows] = useState<Row[]>(() => rowsFromBands(venue.courts[0]?.bands ?? []));
  const [message, setMessage] = useState<{ tone: 'error' | 'success'; text: string }>();
  const [busy, setBusy] = useState(false);

  const selectedCourts = venue.courts.filter((c) => selected.includes(c.id));
  const boardsDiffer = selectedCourts.some((c) => boardKey(c.bands) !== boardKey(selectedCourts[0].bands));
  const update = (key: string, patch: Partial<Row>) => setRows((list) => list.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  const toggle = <T,>(list: T[], item: T) => (list.includes(item) ? list.filter((x) => x !== item) : [...list, item]);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (selected.length === 0) return setMessage({ tone: 'error', text: t('chooseCourts') });
    setBusy(true);
    setMessage(undefined);
    try {
      const updated = await api<Venue>('/venue/prices', {
        method: 'PUT',
        body: { courtIds: selected, bands: bandsFromRows(rows.filter((r) => r.days.length > 0)) },
      });
      onSaved(updated);
      setMessage({ tone: 'success', text: t('saved') });
    } catch (err) {
      setMessage({ tone: 'error', text: errorText(err) });
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="card" onSubmit={save}>
      <fieldset className="mg-fieldset" disabled={!canEdit}>
        <legend>{t('pricesAppliesTo')}</legend>
        <div className="chip-row">
          {venue.courts.map((court) => (
            <label key={court.id} className="check-chip">
              <input
                type="checkbox"
                checked={selected.includes(court.id)}
                onChange={() => setSelected((list) => toggle(list, court.id))}
              />
              {name(court)} · {t('perHour', { price: money(court.hourlyPrice, venue.currencyCode) })}
            </label>
          ))}
        </div>
        {boardsDiffer && <Notice tone="info">{t('pricesDiffer')}</Notice>}
      </fieldset>

      <fieldset className="mg-fieldset" disabled={!canEdit}>
        <legend>{t('peakPeriods')}</legend>
        {rows.length === 0 && <p className="empty">{t('noPeriods')}</p>}
        {rows.map((row) => (
          <div key={row.key} className="period-row">
            <div className="chip-row" role="group" aria-label={t('days')}>
              {DAYS.map((day) => (
                <label key={day} className="check-chip">
                  <input
                    type="checkbox"
                    checked={row.days.includes(day)}
                    onChange={() => update(row.key, { days: DAYS.filter((d) => toggle(row.days, day).includes(d)) })}
                  />
                  {t(DAY_KEY[day])}
                </label>
              ))}
            </div>
            <div className="period-fields">
              <Field label={t('fromTime')}>
                <input type="time" step={900} required value={row.from} onChange={(e) => update(row.key, { from: e.target.value })} />
              </Field>
              <Field label={t('toTime')} hint={row.to <= row.from ? t('closesNextDay') : undefined}>
                <input type="time" step={900} required value={row.to} onChange={(e) => update(row.key, { to: e.target.value })} />
              </Field>
              <Field label={t('pricePerHour')}>
                <input
                  type="number"
                  required
                  min={1}
                  max={10000}
                  step="0.01"
                  inputMode="decimal"
                  value={row.price}
                  onChange={(e) => update(row.key, { price: e.target.value })}
                />
              </Field>
              {canEdit && (
                <Button variant="text" onClick={() => setRows((list) => list.filter((r) => r.key !== row.key))}>
                  {t('remove')}
                </Button>
              )}
            </div>
          </div>
        ))}
        {canEdit && (
          <div>
            <Button
              variant="secondary"
              onClick={() =>
                setRows((list) => [...list, { key: crypto.randomUUID(), days: [], from: '18:00', to: '23:00', price: '' }])
              }
            >
              {t('addPeriod')}
            </Button>
          </div>
        )}
      </fieldset>

      {message && <Notice tone={message.tone}>{message.text}</Notice>}
      {canEdit && (
        <div className="form-actions">
          <Button type="submit" disabled={busy}>
            {t('savePrices')}
          </Button>
        </div>
      )}
    </form>
  );
}

function PriceCheck({ venue, categories }: { venue: Venue; categories: Category[] }) {
  const { t, name, money } = useI18n();
  const [courtId, setCourtId] = useState(venue.courts[0].id);
  const [day, setDay] = useState<Day>('Friday');
  const [time, setTime] = useState('20:00');
  const [minutes, setMinutes] = useState(0);
  const [quote, setQuote] = useState<Quote>();
  const [error, setError] = useState<string>();

  const court = venue.courts.find((c) => c.id === courtId);
  const durations = categories.find((c) => c.id === court?.categoryId)?.durationsMinutes ?? [60];
  const length = durations.includes(minutes) ? minutes : durations[0];

  async function check(e: FormEvent) {
    e.preventDefault();
    setError(undefined);
    try {
      const query = new URLSearchParams({ courtId, day, time, minutes: String(length) });
      setQuote(await api<Quote>(`/venue/quote?${query}`));
    } catch (err) {
      setQuote(undefined);
      setError(errorText(err));
    }
  }

  return (
    <section className="card">
      <h2>{t('priceCheck')}</h2>
      <p className="page-intro">{t('priceCheckIntro')}</p>
      <form className="period-fields" onSubmit={check}>
        <Field label={t('court')}>
          <select value={courtId} onChange={(e) => setCourtId(e.target.value)}>
            {venue.courts.map((c) => (
              <option key={c.id} value={c.id}>
                {name(c)}
              </option>
            ))}
          </select>
        </Field>
        <Field label={t('day')}>
          <select value={day} onChange={(e) => setDay(e.target.value as Day)}>
            {DAYS.map((d) => (
              <option key={d} value={d}>
                {t(DAY_KEY[d])}
              </option>
            ))}
          </select>
        </Field>
        <Field label={t('startTime')}>
          <input type="time" step={900} required value={time} onChange={(e) => setTime(e.target.value)} />
        </Field>
        <Field label={t('length')}>
          <select value={length} onChange={(e) => setMinutes(Number(e.target.value))}>
            {durations.map((m) => (
              <option key={m} value={m}>
                {t('minutesShort', { m })}
              </option>
            ))}
          </select>
        </Field>
        <Button type="submit">{t('check')}</Button>
      </form>
      {error && <Notice tone="error">{error}</Notice>}
      {quote && (
        <div className="quote">
          <p className="quote-line">
            {t('quoteLine', {
              day: t(DAY_KEY[quote.day]),
              time: quote.start.slice(0, 5),
              minutes: quote.minutes,
              total: money(quote.total, quote.currency),
            })}
          </p>
          <p className="hours-note">
            {t('quoteVat', { vat: money(quote.vat, quote.currency), percent: quote.vatPercent })}
          </p>
          <ul className="quote-segments">
            {quote.segments.map((s) => (
              <li key={s.from}>
                {t('segmentLine', {
                  from: s.from.slice(0, 5),
                  to: s.to.slice(0, 5),
                  rate: money(s.hourlyPrice, quote.currency),
                  amount: money(s.amount, quote.currency),
                })}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
