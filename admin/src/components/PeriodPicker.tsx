import { useState } from 'react';
import { CalendarDays, Check, ChevronDown } from 'lucide-react';
import { localToday } from '../lib/greeting';
import { useI18n } from '../lib/i18n';
import { addDays, formatDay } from '../lib/time';
import { useDropdown } from '../lib/useDropdown';

export type Period = { days: 7 | 30 | 90 } | { from: string; to: string };
const PRESETS = [7, 30, 90] as const;
/** Same limit as the API (OverviewEndpoints.MaxDays). */
const MAX_DAYS = 366;

export const periodQuery = (p: Period) => ('days' in p ? `days=${p.days}` : `from=${p.from}&to=${p.to}`);

export function usePeriodLabel() {
  const { t, lang } = useI18n();
  return (p: Period) =>
    'days' in p ? t('lastDays', { n: p.days }) : `${formatDay(p.from, lang, 'short')} – ${formatDay(p.to, lang, 'short')}`;
}

/** The corner date control: a button showing the period; its menu has the presets and a custom from–to range. */
export function PeriodPicker({ value, onChange }: { value: Period; onChange: (p: Period) => void }) {
  const { t } = useI18n();
  const label = usePeriodLabel();
  const menu = useDropdown();
  const today = localToday();
  const [custom, setCustom] = useState(!('days' in value));
  const [from, setFrom] = useState('days' in value ? addDays(today, -29) : value.from);
  const [to, setTo] = useState('days' in value ? today : value.to);
  const span = Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000) + 1;
  const valid = !!from && !!to && from <= to && from <= today && span <= MAX_DAYS;

  function pick(p: Period) {
    onChange(p);
    menu.setOpen(false);
  }

  return (
    <div className="dropdown period-picker" ref={menu.ref}>
      <button
        type="button"
        className="period-button"
        aria-haspopup="dialog"
        aria-expanded={menu.open}
        onClick={() => menu.setOpen(!menu.open)}
      >
        <CalendarDays aria-hidden size={18} />
        <span>{label(value)}</span>
        <ChevronDown aria-hidden size={16} />
      </button>
      {menu.open && (
        <div className="dropdown-panel period-panel" role="dialog" aria-label={t('overviewPeriod')}>
          <ul className="period-options">
            {PRESETS.map((d) => {
              const on = 'days' in value && value.days === d && !custom;
              return (
                <li key={d}>
                  <button type="button" className="dropdown-item" aria-pressed={on} onClick={() => pick({ days: d })}>
                    <span className="period-check">{on && <Check aria-hidden size={16} />}</span>
                    {t('lastDays', { n: d })}
                  </button>
                </li>
              );
            })}
            <li>
              <button type="button" className="dropdown-item" aria-pressed={custom} onClick={() => setCustom(true)}>
                <span className="period-check">{custom && <Check aria-hidden size={16} />}</span>
                {t('customRange')}
              </button>
            </li>
          </ul>
          {custom && (
            <form
              className="period-custom"
              onSubmit={(e) => {
                e.preventDefault();
                if (valid) pick({ from, to });
              }}
            >
              <label className="mg-field">
                <span className="mg-field-label">{t('fromTime')}</span>
                <input type="date" required value={from} max={today} onChange={(e) => setFrom(e.target.value)} />
              </label>
              <label className="mg-field">
                <span className="mg-field-label">{t('toTime')}</span>
                <input type="date" required value={to} min={from} max={today} onChange={(e) => setTo(e.target.value)} />
              </label>
              {from && to && !valid && (
                <p className="period-error" role="alert">
                  {from > to ? t('rangeEndBeforeStart') : t('rangeTooLong', { n: MAX_DAYS })}
                </p>
              )}
              <button type="submit" className="mg-button mg-button--primary" disabled={!valid}>
                {t('apply')}
              </button>
            </form>
          )}
        </div>
      )}
    </div>
  );
}
