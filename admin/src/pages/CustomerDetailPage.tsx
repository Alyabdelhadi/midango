import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { BookingStatusPill } from '../components/BookingStatusPill';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { clock, dayOf, formatDay, formatUtcDate } from '../lib/time';
import { TEAM_STATUS_KEY, TIER_KEY, type AdminCustomer } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';


export function CustomerDetailPage() {
  const { id } = useParams();
  const { t, lang, name, money } = useI18n();
  const date = (iso: string) => formatUtcDate(iso, lang);
  const toast = useToast();
  const customer = useLoad(() => api<AdminCustomer>(`/admin/customers/${id}`));
  const [dialog, setDialog] = useState<'block' | 'lift' | null>(null);
  const [busy, setBusy] = useState(false);
  const c = customer.data;
  const who = c?.name ?? t('noName');

  async function act(action: 'block' | 'unblock' | 'lift-no-show-limit', notice: string, body: unknown = {}) {
    setBusy(true);
    try {
      customer.setData(await api<AdminCustomer>(`/admin/customers/${id}/${action}`, { method: 'POST', body }));
      setDialog(null);
      toast.show('success', notice);
    } catch (err) {
      toast.show('error', errorText(err));
    } finally {
      setBusy(false);
    }
  }

  const venueTime = (iso: string) => `${formatDay(dayOf(iso), lang, 'short')} · ${clock(iso)}`;

  return (
    <div className="page">
      <Link to="/all-customers">{t('allCustomers')}</Link>
      {customer.error && <Notice tone="error">{customer.error}</Notice>}
      {!c && !customer.error && <p className="page-loading">{t('loading')}</p>}
      {c && (
        <>
          <PageHeader
            title={who}
            action={
              <div className="form-actions">
                {(c.limited || c.stats.noShowsInWindow > 0) && (
                  <Button variant="secondary" disabled={busy} onClick={() => setDialog('lift')}>
                    {t('liftLimit')}
                  </Button>
                )}
                {c.blockedAt ? (
                  <Button
                    variant="secondary"
                    disabled={busy}
                    onClick={() => void act('unblock', t('unblockedNoticeName', { name: who }))}
                  >
                    {t('unblockAccount')}
                  </Button>
                ) : (
                  <Button variant="destructive" disabled={busy} onClick={() => setDialog('block')}>
                    {t('blockAccount')}
                  </Button>
                )}
              </div>
            }
          />
          <p className="page-intro">
            <bdi dir="ltr">{c.phone}</bdi> · {t('joined')} {date(c.createdAt)}
          </p>

          {(c.blockedAt || c.limited || c.noShowsClearedAt) && (
            <div className="card customer-status">
              {c.blockedAt && (
                <>
                  <span className="pill-row">
                    <Pill tone="warning">{t('accountBlocked')}</Pill>
                  </span>
                  <p>{t('blockedLine', { by: c.blockedBy ?? '—', when: date(c.blockedAt) })}</p>
                  {c.blockedReason && <p>{t('blockedReasonLine', { reason: c.blockedReason })}</p>}
                </>
              )}
              {c.limited && (
                <>
                  <span className="pill-row">
                    <Pill>{t('accountLimited')}</Pill>
                  </span>
                  <p>{t('limitedLine')}</p>
                </>
              )}
              {c.noShowsClearedAt && <p className="muted">{t('noShowsClearedLine', { when: date(c.noShowsClearedAt) })}</p>}
            </div>
          )}

          <div className="tiles">
            <Tile label={t('tileBookings')} value={c.stats.bookings} />
            <Tile label={t('statusCheckedIn')} value={c.stats.checkedIn} />
            <Tile label={t('tileCancelled')} value={c.stats.cancelled} />
            <Tile label={t('statLateCancellations')} value={c.stats.lateCancellations} />
            <Tile label={t('tileNoShows')} value={c.stats.noShows} />
            <Tile label={t('noShowsInWindow')} value={c.stats.noShowsInWindow} />
            <Tile label={t('statMatchesJoined')} value={c.stats.matchesJoined} />
            <Tile label={t('statTournamentTeams')} value={c.stats.tournamentTeams} />
            <Tile label={t('statDevices')} value={c.devices} />
            <Tile
              label={t('points')}
              value={c.points.balance}
              note={t('tierLine', { tier: t(TIER_KEY[c.points.tier]) })}
            />
          </div>

          <section className="card">
            <h2>{t('latestBookings')}</h2>
            {c.bookings.length === 0 ? (
              <p className="empty">{t('noBookingsYet')}</p>
            ) : (
              <table className="mg-table">
                <thead>
                  <tr>
                    <th>{t('time')}</th>
                    <th>{t('venueColumn')}</th>
                    <th>{t('court')}</th>
                    <th>{t('role')}</th>
                    <th className="numeric">{t('total')}</th>
                    <th>{t('status')}</th>
                  </tr>
                </thead>
                <tbody>
                  {c.bookings.map((b) => (
                    <tr key={b.id}>
                      <td>
                        <span className="nowrap">{venueTime(b.startsAt)}</span>
                        <div className="muted">
                          <bdi dir="ltr">{b.reference}</bdi>
                        </div>
                      </td>
                      <td>{name({ nameEn: b.venueNameEn, nameAr: b.venueNameAr })}</td>
                      <td>{name({ nameEn: b.courtNameEn, nameAr: b.courtNameAr })}</td>
                      <td>{t(b.role === 'organiser' ? 'roleOrganiser' : 'rolePlayer')}</td>
                      <td className="numeric">{money(b.total, b.currency)}</td>
                      <td>
                        <BookingStatusPill booking={b} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          <section className="card">
            <h2>{t('navTournaments')}</h2>
            {c.tournaments.length === 0 ? (
              <p className="empty">{t('noTournamentTeams')}</p>
            ) : (
              <table className="mg-table">
                <thead>
                  <tr>
                    <th>{t('startsAt')}</th>
                    <th>{t('navTournaments')}</th>
                    <th>{t('team')}</th>
                    <th>{t('status')}</th>
                  </tr>
                </thead>
                <tbody>
                  {c.tournaments.map((r) => (
                    <tr key={`${r.tournamentId}|${r.teamName}`}>
                      <td className="nowrap">{venueTime(r.startsAt)}</td>
                      <td>{name(r)}</td>
                      <td>{r.teamName}</td>
                      <td>
                        <Pill tone={r.status === 'Registered' ? 'success' : 'neutral'}>{t(TEAM_STATUS_KEY[r.status])}</Pill>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          <Dialog open={dialog === 'block'} title={t('blockTitle', { name: who })} onClose={() => setDialog(null)}>
            <BlockForm
              busy={busy}
              onCancel={() => setDialog(null)}
              onBlock={(reason) => void act('block', t('blockedNoticeName', { name: who }), { reason })}
            />
          </Dialog>

          <Dialog open={dialog === 'lift'} title={t('liftLimitTitle', { name: who })} onClose={() => setDialog(null)}>
            <div className="dialog-form">
              <p>{t('liftLimitConfirm')}</p>
              <div className="form-actions">
                <Button variant="secondary" onClick={() => setDialog(null)}>
                  {t('cancel')}
                </Button>
                <Button disabled={busy} onClick={() => void act('lift-no-show-limit', t('liftedNotice'))}>
                  {t('liftLimit')}
                </Button>
              </div>
            </div>
          </Dialog>
        </>
      )}
      {toast.view}
    </div>
  );
}

function Tile({ label, value, note }: { label: string; value: number; note?: string }) {
  return (
    <div className="tile">
      <span className="tile-label">{label}</span>
      <span className="tile-value">{value}</span>
      {note && <span className="tile-note">{note}</span>}
    </div>
  );
}

function BlockForm({ busy, onCancel, onBlock }: { busy: boolean; onCancel: () => void; onBlock: (reason: string) => void }) {
  const { t } = useI18n();
  const [reason, setReason] = useState('');
  const [error, setError] = useState(false);

  function submit(e: FormEvent) {
    e.preventDefault();
    const trimmed = reason.trim();
    if (trimmed.length < 3 || trimmed.length > 300) return setError(true);
    onBlock(trimmed);
  }

  return (
    <form className="dialog-form" onSubmit={submit} noValidate>
      <p>{t('blockExplain')}</p>
      <Field label={t('blockReason')} hint={t('blockReasonHint')}>
        <textarea
          value={reason}
          maxLength={300}
          rows={3}
          required
          aria-invalid={error}
          onChange={(e) => {
            setReason(e.target.value);
            setError(false);
          }}
        />
      </Field>
      {error && <Notice tone="error">{t('blockReasonNeeded')}</Notice>}
      <div className="form-actions">
        <Button variant="secondary" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" variant="destructive" disabled={busy}>
          {t('blockAccount')}
        </Button>
      </div>
    </form>
  );
}
