import { useState, type FormEvent } from 'react';
import { Button } from '../../components/Button';
import { useI18n } from '../../lib/i18n';
import { DAY_KEY, DAYS, type Day, type Hours, type Venue } from '../../lib/types';

type Row = { open: boolean; opens: string; closes: string };

function initialRows(venue: Venue): Record<Day, Row> {
  const rows = {} as Record<Day, Row>;
  for (const day of DAYS) {
    const hours = venue.hours.find((h) => h.day === day);
    rows[day] = hours
      ? { open: true, opens: hours.opens.slice(0, 5), closes: hours.closes.slice(0, 5) }
      : { open: false, opens: '16:00', closes: '23:00' };
  }
  return rows;
}

export function HoursSection({
  venue,
  canEdit,
  onSave,
}: {
  venue: Venue;
  canEdit: boolean;
  onSave: (hours: Hours[]) => Promise<void>;
}) {
  const { t } = useI18n();
  const [rows, setRows] = useState(() => initialRows(venue));
  const update = (day: Day, patch: Partial<Row>) => setRows((r) => ({ ...r, [day]: { ...r[day], ...patch } }));

  function submit(e: FormEvent) {
    e.preventDefault();
    void onSave(
      DAYS.filter((day) => rows[day].open).map((day) => ({
        day,
        opens: `${rows[day].opens}:00`,
        closes: `${rows[day].closes}:00`,
      })),
    );
  }

  return (
    <section className="card">
      <h2>{t('openingHours')}</h2>
      <form className="dialog-form" onSubmit={submit}>
        <fieldset className="mg-fieldset" disabled={!canEdit}>
          <legend className="visually-hidden">{t('openingHours')}</legend>
          <table className="mg-table hours-table">
            <thead>
              <tr>
                <th>{t('day')}</th>
                <th>{t('openThisDay')}</th>
                <th>{t('opensAt')}</th>
                <th>{t('closesAt')}</th>
              </tr>
            </thead>
            <tbody>
              {DAYS.map((day) => {
                const row = rows[day];
                return (
                  <tr key={day}>
                    <td>{t(DAY_KEY[day])}</td>
                    <td>
                      <input
                        type="checkbox"
                        aria-label={`${t(DAY_KEY[day])}: ${t('openThisDay')}`}
                        checked={row.open}
                        onChange={(e) => update(day, { open: e.target.checked })}
                      />
                    </td>
                    {row.open ? (
                      <>
                        <td>
                          <input
                            type="time"
                            required
                            aria-label={`${t(DAY_KEY[day])}: ${t('opensAt')}`}
                            value={row.opens}
                            onChange={(e) => update(day, { opens: e.target.value })}
                          />
                        </td>
                        <td>
                          <input
                            type="time"
                            required
                            aria-label={`${t(DAY_KEY[day])}: ${t('closesAt')}`}
                            value={row.closes}
                            onChange={(e) => update(day, { closes: e.target.value })}
                          />{' '}
                          {row.closes <= row.opens && <span className="hours-note">{t('closesNextDay')}</span>}
                        </td>
                      </>
                    ) : (
                      <td colSpan={2} className="hours-note">
                        {t('closedDay')}
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </fieldset>
        {canEdit && (
          <div className="form-actions">
            <Button type="submit">{t('save')}</Button>
          </div>
        )}
      </form>
    </section>
  );
}
