import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { ListToolbar, matches } from '../components/ListToolbar';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api, errorText } from '../lib/api';
import { localToday } from '../lib/greeting';
import { useI18n } from '../lib/i18n';
import type { FeeMode, Statement, StatementRow } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import type { MessageKey } from '../locales/en';

type PaidFilter = 'all' | 'unpaid' | 'paid';
const FILTERS: [PaidFilter, MessageKey][] = [
  ['all', 'filterAll'],
  ['unpaid', 'filterUnpaid'],
  ['paid', 'filterPaid'],
];

export function AccountingPage() {
  const { t } = useI18n();
  const [month, setMonth] = useState(() => localToday().slice(0, 7));
  return (
    <div className="page">
      <PageHeader
        title={t('navAccounting')}
        action={
          <Field label={t('month')}>
            <input type="month" value={month} max={localToday().slice(0, 7)} onChange={(e) => e.target.value && setMonth(e.target.value)} />
          </Field>
        }
      />
      <p className="page-intro">{t('accountingIntro')}</p>
      <StatementView key={month} month={month} />
    </div>
  );
}

function StatementView({ month }: { month: string }) {
  const { t, name, money } = useI18n();
  const toast = useToast();
  const res = useLoad(() => api<Statement>(`/admin/accounting?month=${month}`));
  const [q, setQ] = useState('');
  const [filter, setFilter] = useState<PaidFilter>('all');
  const [planFor, setPlanFor] = useState<StatementRow | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  if (res.error) return <Notice tone="error">{res.error}</Notice>;
  if (!res.data) return <p className="page-loading">{t('loading')}</p>;
  const s = res.data;
  // "Unpaid" means money is actually owed; most owed first, so the ones to chase lead the list.
  const shown = s.rows
    .filter(
      (r) =>
        matches(`${r.nameEn} ${r.nameAr}`, q) &&
        (filter === 'all' || (filter === 'paid' ? r.paidAt !== null : r.paidAt === null && r.net !== 0)),
    )
    .sort((a, b) => Math.abs(b.net) - Math.abs(a.net) || a.nameEn.localeCompare(b.nameEn));

  async function setPaid(row: StatementRow, paid: boolean) {
    setBusyId(row.venueId);
    try {
      await api(`/admin/accounting/statements/${row.venueId}/${month}`, { method: 'PUT', body: { paid } });
      res.reload();
    } catch (err) {
      toast.show('error', errorText(err));
    } finally {
      setBusyId(null);
    }
  }

  const plan = (r: StatementRow) =>
    r.feeMode === 'Fixed'
      ? t('planFixed', { amount: money(r.feeMonthly ?? 0, r.currency) })
      : r.feeMode === 'Percent'
        ? t('planPercent', { pct: r.percent ?? 0 })
        : t('planDefault', { pct: r.percent ?? 0 });

  return (
    <>
      {s.totals.map((total) => (
        <div key={total.currency} className="tiles">
          <Tile label={t('tileCashCollected')} value={money(total.cash, total.currency)} />
          <Tile label={t('tileFeesDue')} value={money(total.fee, total.currency)} />
          <Tile label={t('tilePointsOwed')} value={money(total.pointsOwed, total.currency)} note={t('pointsOwedNote')} />
          <Tile label={t('tileNetDue')} value={money(total.net, total.currency)} />
          <Tile label={t('tileOutstanding')} value={money(total.outstanding, total.currency)} note={t('paidSoFar', { amount: money(total.paid, total.currency) })} />
        </div>
      ))}
      <p className="muted">
        {t('defaultFeeLine', { pct: s.defaultPercent })} <Link to="/rules">{t('changeInSettings')}</Link>
      </p>

      {s.rows.length === 0 ? (
        <p className="empty">{t('noStatementRows')}</p>
      ) : (
        <>
          <ListToolbar
            q={q}
            onQ={setQ}
            placeholder={t('searchVenues')}
            filters={FILTERS}
            filter={filter}
            onFilter={setFilter}
            shown={shown.length}
            total={s.rows.length}
          />
          {shown.length === 0 ? (
            <p className="empty">{t('noMatches')}</p>
          ) : (
            <table className="mg-table">
              <thead>
                <tr>
                  <th scope="col">{t('navVenue')}</th>
                  <th scope="col">{t('plan')}</th>
                  <th scope="col" className="numeric">
                    {t('tileBookings')}
                  </th>
                  <th scope="col" className="numeric">
                    {t('tileCashCollected')}
                  </th>
                  <th scope="col" className="numeric">
                    {t('fee')}
                  </th>
                  <th scope="col" className="numeric">
                    {t('tilePointsOwed')}
                  </th>
                  <th scope="col" className="numeric">
                    {t('net')}
                  </th>
                  <th scope="col">{t('status')}</th>
                  <th scope="col">
                    <span className="visually-hidden">{t('actions')}</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {shown.map((r) => (
                  <tr key={r.venueId}>
                    <th scope="row">{name(r)}</th>
                    <td>
                      <Pill tone={r.feeMode === 'Default' ? 'neutral' : 'info'}>{plan(r)}</Pill>
                    </td>
                    <td className="numeric">{r.bookings}</td>
                    <td className="numeric">{money(r.cash, r.currency)}</td>
                    <td className="numeric">{money(r.fee, r.currency)}</td>
                    <td className="numeric">{money(r.pointsOwed, r.currency)}</td>
                    <td className="numeric">
                      <strong>{money(r.net, r.currency)}</strong>
                    </td>
                    <td>
                      {r.paidAt ? (
                        <Pill tone="success">{t('paidAmount', { amount: money(r.paidAmount ?? 0, r.currency) })}</Pill>
                      ) : r.net === 0 ? (
                        <Pill>{t('nothingDue')}</Pill>
                      ) : (
                        <Pill tone="warning">{t('unpaid')}</Pill>
                      )}
                    </td>
                    <td className="row-actions">
                      {r.paidAt ? (
                        <Button variant="text" disabled={busyId === r.venueId} onClick={() => void setPaid(r, false)}>
                          {t('markUnpaid')}
                        </Button>
                      ) : (
                        r.net !== 0 && (
                          <Button variant="secondary" disabled={busyId === r.venueId} onClick={() => void setPaid(r, true)}>
                            {t('markPaid')}
                          </Button>
                        )
                      )}
                      <Button variant="text" onClick={() => setPlanFor(r)}>
                        {t('changePlan')}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}

      <Dialog open={planFor !== null} title={t('changePlanFor', { venue: planFor ? name(planFor) : '' })} onClose={() => setPlanFor(null)}>
        {planFor && (
          <PlanForm
            row={planFor}
            defaultPercent={s.defaultPercent}
            onCancel={() => setPlanFor(null)}
            onSaved={() => {
              setPlanFor(null);
              res.reload();
            }}
          />
        )}
      </Dialog>
    </>
  );
}

function Tile({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="tile">
      <span className="tile-label">{label}</span>
      <span className="tile-value tile-value--money">{value}</span>
      {note && <span className="tile-note">{note}</span>}
    </div>
  );
}

function PlanForm({
  row,
  defaultPercent,
  onCancel,
  onSaved,
}: {
  row: StatementRow;
  defaultPercent: number;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const { t } = useI18n();
  const [mode, setMode] = useState<FeeMode>(row.feeMode);
  const [percent, setPercent] = useState(row.feeMode === 'Percent' ? String(row.percent ?? '') : '');
  const [fixed, setFixed] = useState(row.feeMonthly !== null ? String(row.feeMonthly) : '');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api(`/admin/accounting/venues/${row.venueId}/plan`, {
        method: 'PUT',
        body: {
          feeMode: mode,
          feePercent: mode === 'Percent' ? Number(percent) : null,
          feeMonthly: mode === 'Fixed' ? Number(fixed) : null,
        },
      });
      onSaved();
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={save}>
      {error && <Notice tone="error">{error}</Notice>}
      <fieldset className="mg-fieldset">
        <legend>{t('plan')}</legend>
        <label className="check-row">
          <input type="radio" name="mode" checked={mode === 'Default'} onChange={() => setMode('Default')} />
          {t('planDefault', { pct: defaultPercent })}
        </label>
        <label className="check-row">
          <input type="radio" name="mode" checked={mode === 'Percent'} onChange={() => setMode('Percent')} />
          {t('planCustomPercent')}
        </label>
        {mode === 'Percent' && (
          <Field label={t('percentOfCash')}>
            <input type="number" required min={0} max={100} step="0.01" value={percent} onChange={(e) => setPercent(e.target.value)} />
          </Field>
        )}
        <label className="check-row">
          <input type="radio" name="mode" checked={mode === 'Fixed'} onChange={() => setMode('Fixed')} />
          {t('planFixedMonthly')}
        </label>
        {mode === 'Fixed' && (
          <Field label={t('amountPerMonth', { currency: row.currency })}>
            <input type="number" required min={0} step="0.01" value={fixed} onChange={(e) => setFixed(e.target.value)} />
          </Field>
        )}
      </fieldset>
      <p className="muted">{t('planAppliesNote')}</p>
      <div className="form-actions">
        <Button variant="secondary" type="button" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t('save')}
        </Button>
      </div>
    </form>
  );
}
