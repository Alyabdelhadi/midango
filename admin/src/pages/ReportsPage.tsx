import { useState } from 'react';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { addDays, formatDay } from '../lib/time';
import { DAY_KEY, DAYS, type Report, type Today } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import type { MessageKey } from '../locales/en';

type Range = 'week' | 'month' | 'quarter' | 'dates';
const RANGES: [Range, MessageKey, number][] = [
  ['week', 'rangeWeek', 7],
  ['month', 'rangeMonth', 28],
  ['quarter', 'rangeQuarter', 91],
  ['dates', 'filterDates', 0],
];

export function ReportsPage() {
  const { t } = useI18n();
  // The venue's business day, so the periods end where the Today page says "today" is.
  const today = useLoad(() => api<Today>('/venue/today'));
  if (today.error) {
    return (
      <div className="page">
        <Notice tone="error">{today.error}</Notice>
      </div>
    );
  }
  if (!today.data) return <p className="page-loading">{t('loading')}</p>;
  return <ReportsView today={today.data.date} />;
}

function ReportsView({ today }: { today: string }) {
  const { t } = useI18n();
  const [range, setRange] = useState<Range>('month');
  const [from, setFrom] = useState(addDays(today, -27));
  const [to, setTo] = useState(today);
  const days = RANGES.find(([r]) => r === range)![2];
  const period = range === 'dates' ? { from, to } : { from: addDays(today, 1 - days), to: today };

  return (
    <div className="page">
      <PageHeader title={t('navReports')} />
      <div className="filter-bar">
        <div className="chip-row" role="radiogroup" aria-label={t('navReports')}>
          {RANGES.map(([value, label]) => (
            <label key={value} className="check-chip">
              <input type="radio" name="range" checked={range === value} onChange={() => setRange(value)} />
              {t(label)}
            </label>
          ))}
        </div>
        {range === 'dates' && (
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
      <ReportBody key={`${period.from}|${period.to}`} from={period.from} to={period.to} />
    </div>
  );
}

function ReportBody({ from, to }: { from: string; to: string }) {
  const { t, money } = useI18n();
  const report = useLoad(() => api<Report>(`/venue/reports?from=${from}&to=${to}`));
  if (report.error) return <Notice tone="error">{report.error}</Notice>;
  if (!report.data) return <p className="page-loading">{t('loading')}</p>;
  const r = report.data;

  return (
    <>
      <div className="tiles">
        <Tile label={t('tileBookings')} value={String(r.bookings)} />
        <Tile label={t('tileOccupancy')} value={`${r.occupancyPercent}%`} />
        <Tile label={t('tileCashTaken')} value={money(r.cashCollected, r.currency)} note={t('checkInsNote', { n: r.checkIns })} />
        <Tile label={t('owedByMidangoPoints')} value={money(r.pointsDiscount, r.currency)} />
        <Tile
          label={t('tileNoShowRate')}
          value={`${r.noShowRatePercent}%`}
          note={t('noShowOf', { n: r.noShows, m: r.played + r.noShows })}
        />
      </div>
      <p className="page-intro">{t('cancellationsLine', { late: r.lateCancellations, venue: r.venueCancellations })}</p>
      <Heatmap report={r} />
      <CashTable report={r} />
    </>
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

const pad = (h: number) => `${String(h % 24).padStart(2, '0')}:00`;

/** Weekday × hour, one navy hue light → dark; every cell prints its percentage and has a hover title. */
function Heatmap({ report: r }: { report: Report }) {
  const { t } = useI18n();
  const cells = new Map(r.heatmap.map((c) => [`${c.day}|${c.hour}`, c]));
  // Hours the venue is ever open, starting at its earliest opening hour and wrapping past midnight.
  const hours = [...new Set(r.heatmap.map((c) => c.hour))].sort(
    (a, b) => ((a - r.dayStartHour + 24) % 24) - ((b - r.dayStartHour + 24) % 24),
  );
  if (hours.length === 0) return null;

  return (
    <section className="card">
      <h2>{t('heatmapTitle')}</h2>
      <p className="page-intro">{t('heatmapIntro')}</p>
      <div className="heatmap-scroll">
        <table className="heatmap">
          <thead>
            <tr>
              <th scope="col">{t('day')}</th>
              {hours.map((h) => (
                <th key={h} scope="col">
                  <bdi dir="ltr">{pad(h)}</bdi>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {DAYS.map((day) => (
              <tr key={day}>
                <th scope="row">{t(DAY_KEY[day])}</th>
                {hours.map((h) => {
                  const c = cells.get(`${day}|${h}`);
                  if (!c) {
                    return (
                      <td key={h} className="heat-closed">
                        {t('closedDay')}
                      </td>
                    );
                  }
                  const title = t('heatCell', {
                    day: t(DAY_KEY[day]),
                    from: pad(h),
                    to: pad(h + 1),
                    pct: c.percent,
                    booked: c.bookedMinutes,
                    open: c.openMinutes,
                  });
                  return (
                    <td
                      key={h}
                      title={title}
                      className={c.percent >= 50 ? 'heat-dark' : undefined}
                      style={{ backgroundColor: `rgb(var(--mg-heat) / ${0.06 + 0.94 * (c.percent / 100)})` }}
                    >
                      {c.percent}%
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="heat-legend" aria-hidden="true">
        <span>0%</span>
        <span className="heat-legend-ramp" />
        <span>100%</span>
      </div>
    </section>
  );
}

/** Weeks down, courts across, totals on both edges. */
function CashTable({ report: r }: { report: Report }) {
  const { t, lang, name, money } = useI18n();
  if (r.cash.length === 0) {
    return (
      <section className="card">
        <h2>{t('cashTitle')}</h2>
        <p className="empty">{t('noCash')}</p>
      </section>
    );
  }
  const courts = [...new Map(r.cash.map((c) => [c.courtId, { nameEn: c.courtNameEn, nameAr: c.courtNameAr }])).entries()];
  const weeks = [...new Set(r.cash.map((c) => c.weekStart))];
  const cell = (week: string, courtId: string) => r.cash.find((c) => c.weekStart === week && c.courtId === courtId)?.cash ?? 0;
  const weekTotal = (week: string) => r.cash.filter((c) => c.weekStart === week).reduce((s, c) => s + c.cash, 0);
  const courtTotal = (courtId: string) => r.cash.filter((c) => c.courtId === courtId).reduce((s, c) => s + c.cash, 0);
  const weekPoints = (week: string) => r.cash.filter((c) => c.weekStart === week).reduce((s, c) => s + c.pointsDiscount, 0);

  return (
    <section className="card">
      <h2>{t('cashTitle')}</h2>
      <table className="mg-table">
        <thead>
          <tr>
            <th scope="col">{t('date')}</th>
            {courts.map(([id, court]) => (
              <th key={id} scope="col" className="numeric">
                {name(court)}
              </th>
            ))}
            <th scope="col" className="numeric">
              {t('total')}
            </th>
            <th scope="col" className="numeric">
              {t('owedByMidangoPoints')}
            </th>
          </tr>
        </thead>
        <tbody>
          {weeks.map((week) => (
            <tr key={week}>
              <th scope="row">{t('weekOf', { date: formatDay(week, lang, 'short') })}</th>
              {courts.map(([id]) => (
                <td key={id} className="numeric">
                  {money(cell(week, id), r.currency)}
                </td>
              ))}
              <td className="numeric">{money(weekTotal(week), r.currency)}</td>
              <td className="numeric">{money(weekPoints(week), r.currency)}</td>
            </tr>
          ))}
          <tr className="total-row">
            <th scope="row">{t('total')}</th>
            {courts.map(([id]) => (
              <td key={id} className="numeric">
                {money(courtTotal(id), r.currency)}
              </td>
            ))}
            <td className="numeric">{money(r.cashCollected, r.currency)}</td>
            <td className="numeric">{money(r.pointsDiscount, r.currency)}</td>
          </tr>
        </tbody>
      </table>
    </section>
  );
}
