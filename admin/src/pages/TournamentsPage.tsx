import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { Button } from '../components/Button';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { clock, dayOf, formatDay } from '../lib/time';
import { FORMAT_KEY, TOURNAMENT_STATUS_KEY, type TournamentStatus, type TournamentSummary } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { ListToolbar, matches } from '../components/ListToolbar';
import type { MessageKey } from '../locales/en';
import { TournamentDialog } from './TournamentDialog';

/** "Thu 19 Nov 09:00–18:00", or "Thu 19 Nov 09:00 – Fri 20 Nov 18:00" across days. Venue wall-clock time. */
export function TournamentDates({ startsAt, endsAt }: { startsAt: string; endsAt: string }) {
  const { lang } = useI18n();
  const day = (iso: string) => formatDay(dayOf(iso), lang, 'short');
  return dayOf(startsAt) === dayOf(endsAt) ? (
    <>
      {day(startsAt)} · <bdi dir="ltr">{clock(startsAt)}–{clock(endsAt)}</bdi>
    </>
  ) : (
    <>
      {day(startsAt)} <bdi dir="ltr">{clock(startsAt)}</bdi> – {day(endsAt)} <bdi dir="ltr">{clock(endsAt)}</bdi>
    </>
  );
}

export function TournamentStatusPill({ status }: { status: TournamentStatus }) {
  const { t } = useI18n();
  return (
    <Pill tone={status === 'Published' || status === 'InProgress' ? 'success' : status === 'Cancelled' ? 'warning' : 'neutral'}>
      {t(TOURNAMENT_STATUS_KEY[status])}
    </Pill>
  );
}

type TournamentFilter = 'all' | 'upcoming' | 'live' | 'finished' | 'cancelled';
const TOURNAMENT_FILTERS: [TournamentFilter, MessageKey][] = [
  ['all', 'filterAll'],
  ['upcoming', 'filterUpcoming'],
  ['live', 'tournamentInProgress'],
  ['finished', 'tournamentFinished'],
  ['cancelled', 'tournamentCancelled'],
];
const TOURNAMENT_FILTER_STATUS: Record<'live' | 'finished' | 'cancelled', TournamentStatus> = {
  live: 'InProgress',
  finished: 'Finished',
  cancelled: 'Cancelled',
};

export function TournamentsPage() {
  const { t, lang, name, money } = useI18n();
  const { staff } = useAuth();
  const navigate = useNavigate();
  const canManage = staff?.role === 'VenueManager';
  const list = useLoad(() => api<TournamentSummary[]>('/venue/tournaments'));
  const [creating, setCreating] = useState(false);
  const rows = list.data;
  const [q, setQ] = useState('');
  const [state, setState] = useState<TournamentFilter>('all');
  const shown = rows?.filter(
    (r) =>
      matches(`${r.nameEn} ${r.nameAr}`, q) &&
      (state === 'all' ||
        (state === 'upcoming' ? r.status === 'Draft' || r.status === 'Published' : r.status === TOURNAMENT_FILTER_STATUS[state])),
  );

  return (
    <div className="page">
      <PageHeader
        title={t('navTournaments')}
        action={canManage && <Button onClick={() => setCreating(true)}>{t('newTournament')}</Button>}
      />
      <p className="page-intro">{t('tournamentsIntro')}</p>
      {list.error && <Notice tone="error">{list.error}</Notice>}
      {!rows && !list.error && <p className="page-loading">{t('loading')}</p>}
      {rows?.length === 0 && <p className="empty">{t('noTournaments')}</p>}
      {rows && rows.length > 0 && (
        <ListToolbar
          q={q}
          onQ={setQ}
          placeholder={t('searchTournaments')}
          filters={TOURNAMENT_FILTERS}
          filter={state}
          onFilter={setState}
          shown={shown!.length}
          total={rows.length}
        />
      )}
      {shown?.length === 0 && rows!.length > 0 && <p className="empty">{t('noMatches')}</p>}
      {shown && shown.length > 0 && (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('tournament')}</th>
              <th>{t('dates')}</th>
              <th>{t('registrationCloses')}</th>
              <th className="numeric">{t('teams')}</th>
              <th className="numeric">{t('entryFee')}</th>
              <th>{t('status')}</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((r) => (
              <tr key={r.id}>
                <td>
                  <Link to={`/tournaments/${r.id}`}>{name(r)}</Link>
                  <br />
                  <span className="muted">{t(FORMAT_KEY[r.format])}</span>
                </td>
                <td>
                  <TournamentDates startsAt={r.startsAt} endsAt={r.endsAt} />
                </td>
                <td>
                  {formatDay(dayOf(r.registrationClosesAt), lang, 'short')} · <bdi dir="ltr">{clock(r.registrationClosesAt)}</bdi>
                </td>
                <td className="numeric">
                  <bdi dir="ltr">
                    {r.registered}/{r.maxTeams}
                  </bdi>
                  {r.waitlisted > 0 && <span className="muted"> {t('waitingCount', { n: r.waitlisted })}</span>}
                </td>
                <td className="numeric">{money(r.entryFee, r.currency)}</td>
                <td>
                  <TournamentStatusPill status={r.status} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <TournamentDialog
        open={creating}
        onClose={() => setCreating(false)}
        onSaved={(created) => {
          setCreating(false);
          navigate(`/tournaments/${created.id}`);
        }}
      />
    </div>
  );
}
