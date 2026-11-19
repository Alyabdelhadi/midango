import { useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { ArrowDownRight, ArrowUpRight, Banknote, CalendarCheck, UserPlus, UserX, type LucideIcon } from 'lucide-react';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { PeriodPicker, periodQuery, usePeriodLabel, type Period } from '../components/PeriodPicker';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { formatDay } from '../lib/time';
import type { Overview, OverviewDay } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { localToday, useGreeting } from '../lib/greeting';
import { initials } from '../layout/nav';
import type { MessageKey } from '../locales/en';

/** Validated categorical slots (dataviz reference palette), assigned in fixed order. "Other" is neutral gray. */
const SERIES = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4'];
const OTHER = '#9aa1ad';

export function OverviewPage() {
  const { t, lang } = useI18n();
  const greeting = useGreeting();
  const [period, setPeriod] = useState<Period>({ days: 30 });
  const query = periodQuery(period);
  return (
    <div className="page">
      <div className="dash-head">
        <PageHeader title={greeting} action={<PeriodPicker value={period} onChange={setPeriod} />} />
        <p className="page-intro">
          {formatDay(localToday(), lang)} · {t('overviewIntro')}
        </p>
      </div>
      <OverviewBody key={query} query={query} period={period} />
    </div>
  );
}

function OverviewBody({ query, period }: { query: string; period: Period }) {
  const { t, money, name, lang } = useI18n();
  const periodLabel = usePeriodLabel();
  const res = useLoad(() => api<Overview>(`/admin/overview?${query}`));
  if (res.error) return <Notice tone="error">{res.error}</Notice>;
  if (!res.data) return <p className="page-loading">{t('loading')}</p>;
  const o = res.data;
  const cash = o.cash.find((c) => c.currency === o.mainCurrency)?.amount ?? 0;
  const pct = (part: number, whole: number) => (whole === 0 ? 0 : Math.round((100 * part) / whole));

  return (
    <>
      <div className="kpis">
        <Kpi
          icon={CalendarCheck}
          color={SERIES[0]}
          label={t('tileBookingsMade')}
          value={String(o.bookingsMade)}
          now={o.bookingsMade}
          before={o.previous.bookingsMade}
          spark={o.series.map((d) => d.bookings)}
        />
        <Kpi
          icon={UserPlus}
          color={SERIES[2]}
          label={t('tileNewCustomers')}
          value={String(o.newCustomers)}
          now={o.newCustomers}
          before={o.previous.newCustomers}
          spark={o.series.map((d) => d.newCustomers)}
        />
        <Kpi
          icon={Banknote}
          color="#008300"
          label={t('tileCashTaken')}
          value={money(cash, o.mainCurrency)}
          now={cash}
          before={o.previous.cash}
          spark={o.series.map((d) => d.cash)}
        />
        <Kpi
          icon={UserX}
          color={SERIES[1]}
          label={t('tileNoShowRate')}
          value={`${o.noShowRatePercent}%`}
          now={o.noShowRatePercent}
          before={o.previous.noShowRatePercent}
          spark={o.series.map((d) => d.noShows)}
          lowerIsBetter
        />
      </div>

      <div className="dash-row dash-row--wide">
        <section className="card">
          <CardHead title={t('chartActivity')} sub={t('chartActivitySub', { period: periodLabel(period) })} />
          <TrendChart
            series={o.series}
            lines={[
              { label: t('tileBookingsMade'), color: SERIES[0], pick: (d) => d.bookings },
              { label: t('tileCheckIns'), color: SERIES[1], pick: (d) => d.checkIns },
            ]}
          />
        </section>
        <section className="card">
          <CardHead title={t('chartBySport')} sub={t('chartBySportSub')} />
          <SportDonut items={o.bySport.map((s) => ({ label: name(s), value: s.bookings }))} total={o.bookingsMade} />
        </section>
      </div>

      <div className="dash-row dash-row--wide">
        <section className="card card--flush">
          <CardHead title={t('topVenues')} sub={t('topVenuesSub')} />
          {o.topVenues.length === 0 ? (
            <p className="empty card-pad">{t('noBookingsInPeriod')}</p>
          ) : (
            <table className="mg-table">
              <thead>
                <tr>
                  <th scope="col">{t('navVenue')}</th>
                  <th scope="col" className="numeric">{t('tileBookingsMade')}</th>
                  <th scope="col" className="numeric">{t('tileCheckIns')}</th>
                  <th scope="col" className="numeric">{t('noShowsCol')}</th>
                  <th scope="col" className="numeric">{t('showUpRate')}</th>
                </tr>
              </thead>
              <tbody>
                {o.topVenues.map((v) => (
                  <tr key={v.id}>
                    <th scope="row">{name(v)}</th>
                    <td className="numeric">{v.bookings}</td>
                    <td className="numeric">{v.checkIns}</td>
                    <td className="numeric">{v.noShows}</td>
                    <td className="numeric">
                      {v.checkIns + v.noShows === 0 ? '—' : `${pct(v.checkIns, v.checkIns + v.noShows)}%`}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
        <section className="card">
          <CardHead title={t('chartNewCustomersPerDay')} sub={t('totalN', { n: o.newCustomers })} />
          <Bars series={o.series} pick={(d) => d.newCustomers} color="var(--mg-navy)" />
        </section>
      </div>

      <div className="dash-row dash-row--wide">
        <section className="card card--flush">
          <CardHead title={t('recentBookings')} sub={t('recentBookingsSub')} />
          <table className="mg-table">
            <thead>
              <tr>
                <th scope="col">{t('customerCol')}</th>
                <th scope="col">{t('navVenue')}</th>
                <th scope="col">{t('statusCol')}</th>
                <th scope="col" className="numeric">{t('playsOn')}</th>
              </tr>
            </thead>
            <tbody>
              {o.recent.map((b) => {
                const who = b.customerName ?? t('guest');
                return (
                  <tr key={b.id}>
                    <th scope="row">
                      <span className="who">
                        <span className="avatar" aria-hidden="true">
                          {initials(who)}
                        </span>
                        <span>
                          {who}
                          <small>
                            <bdi dir="ltr">{b.reference}</bdi> · {t(b.source === 'App' ? 'viaApp' : 'viaDesk')}
                          </small>
                        </span>
                      </span>
                    </th>
                    <td>{name({ nameEn: b.venueNameEn, nameAr: b.venueNameAr })}</td>
                    <td>
                      <StatusPill status={b.status} checkedIn={b.checkedIn} />
                    </td>
                    <td className="numeric">{formatDay(b.startsAt.slice(0, 10), lang, 'short')}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </section>
        <section className="card">
          <CardHead title={t('platformHealth')} sub={t('platformHealthSub')} />
          <Meter label={t('checkInRate')} now={o.checkIns} of={o.played} color={SERIES[0]} />
          <Meter label={t('venuesLiveShare')} now={o.venuesLive} of={o.venuesLive + o.venuesDraft} color={SERIES[2]} />
          <Meter label={t('appShare')} now={o.appBookings} of={o.bookingsMade} color={SERIES[1]} />
          <dl className="health-facts">
            <Fact label={t('tileUpcoming')} value={o.upcomingBookings} />
            <Fact label={t('tileActiveCourts')} value={o.courtsActive} />
            <Fact label={t('tileCustomers')} value={o.customers} />
            <Fact
              label={t('tileLeadsWaiting')}
              value={o.leadsWaiting > 0 ? <Link to="/leads">{o.leadsWaiting}</Link> : 0}
            />
          </dl>
        </section>
      </div>
    </>
  );
}

function CardHead({ title, sub }: { title: string; sub?: string }) {
  return (
    <header className="dash-card-head">
      <h2>{title}</h2>
      {sub && <p>{sub}</p>}
    </header>
  );
}

function StatusPill({ status, checkedIn }: { status: Overview['recent'][number]['status']; checkedIn: boolean }) {
  const { t } = useI18n();
  const [tone, key]: [string, MessageKey] = checkedIn
    ? ['success', 'statusCheckedIn']
    : status === 'NoShow'
      ? ['warning', 'statusNoShow']
      : status === 'Cancelled'
        ? ['neutral', 'statusCancelled']
        : ['info', 'statusConfirmed'];
  return <span className={`mg-pill mg-pill--${tone}`}>{t(key)}</span>;
}

function Fact({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function Meter({ label, now, of, color }: { label: string; now: number; of: number; color: string }) {
  const p = of === 0 ? 0 : Math.round((100 * now) / of);
  return (
    <div className="meter">
      <div className="meter-top">
        <span>{label}</span>
        <span className="meter-value">
          {p}% <small>
            {now} / {of}
          </small>
        </span>
      </div>
      <div className="meter-track" role="progressbar" aria-label={label} aria-valuenow={p} aria-valuemin={0} aria-valuemax={100}>
        <div className="meter-fill" style={{ width: `${p}%`, background: color }} />
      </div>
    </div>
  );
}

/** Change vs the previous period: an arrow + text, green when it moved the good way. */
function Kpi(props: {
  icon: LucideIcon;
  color: string;
  label: string;
  value: string;
  now: number;
  before: number;
  spark: number[];
  lowerIsBetter?: boolean;
}) {
  const { t } = useI18n();
  const { icon: Icon, color, now, before } = props;
  const change = before === 0 ? null : Math.round((1000 * (now - before)) / before) / 10;
  const good = change === null || change === 0 ? null : (change > 0) !== !!props.lowerIsBetter;
  const Arrow = change !== null && change < 0 ? ArrowDownRight : ArrowUpRight;
  return (
    <section className="kpi">
      <div className="kpi-top">
        <span className="kpi-label">{props.label}</span>
        <span className="kpi-icon" style={{ color, background: `color-mix(in srgb, ${color} 12%, transparent)` }}>
          <Icon aria-hidden size={18} />
        </span>
      </div>
      <span className="kpi-value">{props.value}</span>
      <span className={`kpi-change${good === null ? '' : good ? ' kpi-change--good' : ' kpi-change--bad'}`}>
        {change === null ? (
          t(now === 0 ? 'noChange' : 'newThisPeriod')
        ) : (
          <>
            <Arrow aria-hidden size={14} />
            {t('vsPrevious', { pct: `${change > 0 ? '+' : ''}${change}%` })}
          </>
        )}
      </span>
      <Sparkline values={props.spark} color={color} />
    </section>
  );
}

function Sparkline({ values, color }: { values: number[]; color: string }) {
  const w = 240;
  const h = 44;
  const max = Math.max(1, ...values);
  const pts = values.map((v, i) => [(i / Math.max(1, values.length - 1)) * w, h - 2 - (v / max) * (h - 6)] as const);
  const line = pts.map(([x, y], i) => `${i ? 'L' : 'M'}${x.toFixed(1)},${y.toFixed(1)}`).join('');
  const id = `spark-${color.replace(/[^a-z0-9]/gi, '')}`;
  return (
    <svg className="sparkline" viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" aria-hidden="true">
      <defs>
        <linearGradient id={id} x1="0" x2="0" y1="0" y2="1">
          <stop offset="0" stopColor={color} stopOpacity="0.22" />
          <stop offset="1" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={`${line}L${w},${h}L0,${h}Z`} fill={`url(#${id})`} />
      <path d={line} fill="none" stroke={color} strokeWidth="2" vectorEffect="non-scaling-stroke" />
    </svg>
  );
}

function useWidth<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(600);
  useLayoutEffect(() => {
    if (!ref.current) return;
    const ro = new ResizeObserver(([e]) => setWidth(e.contentRect.width));
    ro.observe(ref.current);
    return () => ro.disconnect();
  }, []);
  return [ref, width] as const;
}

/** Rounds up to 1/2/5 × 10ⁿ so the gridlines land on readable numbers. */
function niceMax(v: number) {
  if (v <= 4) return 4;
  const p = 10 ** Math.floor(Math.log10(v));
  return [1, 2, 5, 10].map((m) => m * p).find((m) => m >= v)!;
}

type Line = { label: string; color: string; pick: (d: OverviewDay) => number };

/** Day-by-day lines on one axis, a light area under the first, crosshair + tooltip on hover. */
function TrendChart({ series, lines }: { series: OverviewDay[]; lines: Line[] }) {
  const { t, lang } = useI18n();
  const [ref, width] = useWidth<HTMLDivElement>();
  const [hover, setHover] = useState<number | null>(null);
  const h = 260;
  const pad = { l: 36, r: 8, t: 10, b: 26 };
  const iw = Math.max(10, width - pad.l - pad.r);
  const ih = h - pad.t - pad.b;
  const max = niceMax(Math.max(...lines.flatMap((l) => series.map(l.pick))));
  const x = (i: number) => pad.l + (series.length === 1 ? iw / 2 : (i / (series.length - 1)) * iw);
  const y = (v: number) => pad.t + ih - (v / max) * ih;
  const path = (l: Line) => series.map((d, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(l.pick(d)).toFixed(1)}`).join('');
  const ticks = [0, 1, 2, 3, 4].map((k) => (max * k) / 4);
  const every = Math.ceil(series.length / 6);
  const label = (d: OverviewDay) => formatDay(d.date, lang, 'short');

  return (
    <div className="trend">
      <ul className="legend">
        {lines.map((l) => (
          <li key={l.label}>
            <span className="legend-dot" style={{ background: l.color }} />
            {l.label}
          </li>
        ))}
      </ul>
      <div ref={ref} className="trend-plot" onMouseLeave={() => setHover(null)}>
        <svg width={width} height={h} role="img" aria-label={lines.map((l) => l.label).join(', ')}>
          <defs>
            <linearGradient id="trend-fill" x1="0" x2="0" y1="0" y2="1">
              <stop offset="0" stopColor={lines[0].color} stopOpacity="0.16" />
              <stop offset="1" stopColor={lines[0].color} stopOpacity="0" />
            </linearGradient>
          </defs>
          {ticks.map((v) => (
            <g key={v}>
              <line x1={pad.l} x2={pad.l + iw} y1={y(v)} y2={y(v)} className="grid" />
              <text x={pad.l - 8} y={y(v)} className="axis" textAnchor="end" dominantBaseline="middle">
                {Math.round(v)}
              </text>
            </g>
          ))}
          {series.map((d, i) =>
            i % every === 0 || i === series.length - 1 ? (
              <text key={d.date} x={x(i)} y={h - 6} className="axis" textAnchor="middle">
                {label(d)}
              </text>
            ) : null,
          )}
          <path d={`${path(lines[0])}L${x(series.length - 1)},${y(0)}L${x(0)},${y(0)}Z`} fill="url(#trend-fill)" />
          {lines.map((l) => (
            <path key={l.label} d={path(l)} fill="none" stroke={l.color} strokeWidth="2" strokeLinejoin="round" />
          ))}
          {hover !== null && (
            <g>
              <line x1={x(hover)} x2={x(hover)} y1={pad.t} y2={pad.t + ih} className="crosshair" />
              {lines.map((l) => (
                <circle key={l.label} cx={x(hover)} cy={y(l.pick(series[hover]))} r="5" fill={l.color} className="dot" />
              ))}
            </g>
          )}
          <rect
            x={pad.l}
            y={pad.t}
            width={iw}
            height={ih}
            fill="transparent"
            onMouseMove={(e) => {
              const box = e.currentTarget.getBoundingClientRect();
              const ratio = (e.clientX - box.left) / box.width;
              setHover(Math.min(series.length - 1, Math.max(0, Math.round(ratio * (series.length - 1)))));
            }}
          />
        </svg>
        {hover !== null && (
          <div
            className="chart-tip"
            style={{ left: Math.min(Math.max(x(hover), 90), width - 90), top: pad.t }}
            role="status"
          >
            <strong>{label(series[hover])}</strong>
            {lines.map((l) => (
              <span key={l.label}>
                <span className="legend-dot" style={{ background: l.color }} />
                {l.label}: <b>{l.pick(series[hover])}</b>
              </span>
            ))}
          </div>
        )}
      </div>
      <details>
        <summary>{t('showAsTable')}</summary>
        <table className="mg-table">
          <thead>
            <tr>
              <th scope="col">{t('date')}</th>
              {lines.map((l) => (
                <th key={l.label} scope="col" className="numeric">
                  {l.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {series.map((d) => (
              <tr key={d.date}>
                <th scope="row">{label(d)}</th>
                {lines.map((l) => (
                  <td key={l.label} className="numeric">
                    {l.pick(d)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </div>
  );
}

/** Top five sports in fixed slot order, the rest folded into "Other"; legend carries name + count. */
function SportDonut({ items, total }: { items: { label: string; value: number }[]; total: number }) {
  const { t } = useI18n();
  if (total === 0 || items.length === 0) return <p className="empty">{t('noBookingsInPeriod')}</p>;
  const top = items.slice(0, SERIES.length).map((s, i) => ({ ...s, color: SERIES[i] }));
  const rest = items.slice(SERIES.length).reduce((s, x) => s + x.value, 0);
  const parts = rest > 0 ? [...top, { label: t('other'), value: rest, color: OTHER }] : top;
  const sum = parts.reduce((s, p) => s + p.value, 0);
  const r = 80;
  const c = 2 * Math.PI * r;
  const gap = parts.length > 1 ? 3 : 0;
  let offset = 0;

  return (
    <div className="donut">
      <svg viewBox="0 0 220 220" role="img" aria-label={t('chartBySport')}>
        <g transform="rotate(-90 110 110)">
          {parts.map((p) => {
            const len = (p.value / sum) * c;
            const el = (
              <circle
                key={p.label}
                cx="110"
                cy="110"
                r={r}
                fill="none"
                stroke={p.color}
                strokeWidth="30"
                strokeDasharray={`${Math.max(0, len - gap)} ${c}`}
                strokeDashoffset={-offset}
              >
                <title>
                  {p.label}: {p.value}
                </title>
              </circle>
            );
            offset += len;
            return el;
          })}
        </g>
        <text x="110" y="100" textAnchor="middle" className="donut-caption">
          {t('total')}
        </text>
        <text x="110" y="128" textAnchor="middle" className="donut-total">
          {sum}
        </text>
      </svg>
      <ul className="legend legend--stack">
        {parts.map((p) => (
          <li key={p.label}>
            <span className="legend-dot" style={{ background: p.color }} />
            <span className="legend-name">{p.label}</span>
            <span className="legend-value">
              {p.value} <small>({Math.round((100 * p.value) / sum)}%)</small>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** One series per day: hover a bar for its value; gridlines at readable steps. */
function Bars({ series, pick, color }: { series: OverviewDay[]; pick: (d: OverviewDay) => number; color: string }) {
  const { t, lang } = useI18n();
  const max = niceMax(Math.max(...series.map(pick)));
  const label = (d: OverviewDay) => formatDay(d.date, lang, 'short');
  const ticks = [4, 3, 2, 1, 0].map((k) => (max * k) / 4);

  return (
    <div className="vbars">
      <div className="vbars-y" aria-hidden="true">
        {ticks.map((v) => (
          <span key={v}>{Math.round(v)}</span>
        ))}
      </div>
      <div className="vbars-plot" aria-hidden="true" style={{ gridTemplateColumns: `repeat(${series.length}, 1fr)` }}>
        {series.map((d) => (
          <div key={d.date} className="bar-slot">
            <div className="bar" style={{ height: `${(pick(d) / max) * 100}%`, background: color }} />
            <span className="bar-tip">
              {label(d)}: <strong>{pick(d)}</strong>
            </span>
          </div>
        ))}
      </div>
      <div className="vbars-x" aria-hidden="true">
        <span>{label(series[0])}</span>
        <span>{label(series[series.length - 1])}</span>
      </div>
      <details className="vbars-table">
        <summary>{t('showAsTable')}</summary>
        <table className="mg-table">
          <tbody>
            {series.map((d) => (
              <tr key={d.date}>
                <th scope="row">{label(d)}</th>
                <td className="numeric">{pick(d)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </div>
  );
}
