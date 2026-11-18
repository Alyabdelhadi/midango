import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { Button } from '../components/Button';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api, ApiError, errorText } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { clock, dayOf, formatDay, recent } from '../lib/time';
import {
  FORMAT_KEY,
  LEVEL_KEY,
  type TeamStatus,
  type Tournament,
  type TournamentClash,
  type TournamentTeam,
} from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import type { MessageKey } from '../locales/en';
import { TournamentDialog } from './TournamentDialog';
import { TournamentDates, TournamentStatusPill } from './TournamentsPage';

const HOUR = 3_600_000;

const GROUPS: [TeamStatus, MessageKey][] = [
  ['Registered', 'teamsRegistered'],
  ['Waitlisted', 'teamsWaitlist'],
  ['Withdrawn', 'teamsWithdrawn'],
];

/** One tournament: summary, the manager's Edit / Publish / Cancel, and the teams with desk check-in. */
export function TournamentPage() {
  const { id } = useParams();
  const { t, lang, name, money } = useI18n();
  const { staff } = useAuth();
  const canManage = staff?.role === 'VenueManager';
  const tournament = useLoad(() => api<Tournament>(`/venue/tournaments/${id}`));
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const [clashes, setClashes] = useState<TournamentClash[]>();
  const [busy, setBusy] = useState(false);

  async function act(path: string, message: string) {
    setBusy(true);
    setClashes(undefined);
    try {
      tournament.setData(await api<Tournament>(`/venue/tournaments/${id}/${path}`, { method: 'POST' }));
      setConfirmingCancel(false);
      toast.show('success', message);
    } catch (err) {
      if (err instanceof ApiError && Array.isArray(err.problem.clashes)) setClashes(err.problem.clashes as TournamentClash[]);
      toast.show('error', errorText(err));
    } finally {
      setBusy(false);
    }
  }

  if (tournament.error) {
    return (
      <div className="page">
        <Notice tone="error">{tournament.error}</Notice>
      </div>
    );
  }
  if (!tournament.data) return <p className="page-loading">{t('loading')}</p>;
  const tr = tournament.data;
  const live = tr.status !== 'Cancelled';

  return (
    <div className="page">
      <p>
        <Link to="/tournaments">{t('allTournaments')}</Link>
      </p>
      <PageHeader
        title={name(tr)}
        action={
          canManage &&
          live && (
            <Button variant="secondary" onClick={() => setEditing(true)}>
              {t('edit')}
            </Button>
          )
        }
      />

      <section className="card">
        <dl className="details">
          <dt>{t('status')}</dt>
          <dd>
            <TournamentStatusPill status={tr.status} />
          </dd>
          <dt>{t('dates')}</dt>
          <dd>
            <TournamentDates startsAt={tr.startsAt} endsAt={tr.endsAt} />
          </dd>
          <dt>{t('registrationCloses')}</dt>
          <dd>
            {formatDay(dayOf(tr.registrationClosesAt), lang, 'short')} · <bdi dir="ltr">{clock(tr.registrationClosesAt)}</bdi>
          </dd>
          <dt>{t('format')}</dt>
          <dd>
            {t(FORMAT_KEY[tr.format])}
            <span className="muted"> — {t(tr.format === 'Knockout' ? 'formatKnockoutHint' : 'formatRoundRobinHint')}</span>
          </dd>
          <dt>{t('courts')}</dt>
          <dd>{tr.courts.map((c) => name(c)).join(', ')}</dd>
          <dt>{t('teamSize')}</dt>
          <dd className="numeric">{tr.teamSize}</dd>
          <dt>{t('teams')}</dt>
          <dd>
            <bdi dir="ltr">
              {tr.teams.filter((x) => x.status === 'Registered').length}/{tr.maxTeams}
            </bdi>
          </dd>
          <dt>{t('entryFee')}</dt>
          <dd>{money(tr.entryFee, tr.currency)}</dd>
          <dt>{t('level')}</dt>
          <dd>{tr.level ? t(LEVEL_KEY[tr.level]) : t('levelAny')}</dd>
          {(lang === 'ar' ? tr.descriptionAr : tr.descriptionEn) && (
            <>
              <dt>{t('description')}</dt>
              <dd className="pre-line">{lang === 'ar' ? tr.descriptionAr : tr.descriptionEn}</dd>
            </>
          )}
        </dl>

        {canManage && tr.status === 'Draft' && (
          <div className="panel-section">
            <p className="muted">{t('publishTournamentHint')}</p>
            <div>
              <Button disabled={busy} onClick={() => void act('publish', t('tournamentPublishedNotice'))}>
                {t('publish')}
              </Button>
            </div>
          </div>
        )}
        {clashes && clashes.length > 0 && (
          <ul className="clash-list" role="alert">
            {clashes.map((c) => (
              <li key={`${c.reference}-${c.courtNameEn}`}>
                {name({ nameEn: c.courtNameEn, nameAr: c.courtNameAr })} · {formatDay(dayOf(c.startsAt), lang, 'short')}{' '}
                <bdi dir="ltr">
                  {clock(c.startsAt)}–{clock(c.endsAt)}
                </bdi>{' '}
                · <bdi dir="ltr">{c.reference}</bdi>
              </li>
            ))}
          </ul>
        )}
        {canManage &&
          live &&
          (confirmingCancel ? (
            <div className="panel-section">
              <p>{t('confirmCancelTournament')}</p>
              <div className="form-actions">
                <Button variant="secondary" onClick={() => setConfirmingCancel(false)}>
                  {t('keepIt')}
                </Button>
                <Button variant="destructive" disabled={busy} onClick={() => void act('cancel', t('tournamentCancelledNotice'))}>
                  {t('yesCancel')}
                </Button>
              </div>
            </div>
          ) : (
            <div>
              <Button variant="secondary" onClick={() => setConfirmingCancel(true)}>
                {t('cancelTournament')}
              </Button>
            </div>
          ))}
      </section>

      {tr.teams.length === 0 ? (
        <section className="card">
          <h2>{t('teams')}</h2>
          <p className="empty">{t('noTeamsYet')}</p>
        </section>
      ) : (
        GROUPS.map(([status, label]) => {
          const teams = tr.teams.filter((x) => x.status === status);
          if (teams.length === 0) return null;
          return (
            <section key={status} className="card">
              <h2>{t(label, { n: teams.length })}</h2>
              <TeamsTable
                tournament={tr}
                teams={teams}
                onChanged={(updated, message, undo) => {
                  tournament.setData(updated);
                  toast.show(
                    'success',
                    message,
                    undo &&
                      (async () => {
                        tournament.setData(await undo());
                      }),
                  );
                }}
                onError={(text) => toast.show('error', text)}
              />
            </section>
          );
        })
      )}

      <TournamentDialog
        open={editing}
        initial={tr}
        onClose={() => setEditing(false)}
        onSaved={(saved) => {
          setEditing(false);
          tournament.setData(saved);
          toast.show('success', t('saved'));
        }}
      />
      {toast.view}
    </div>
  );
}

function TeamsTable({
  tournament: tr,
  teams,
  onChanged,
  onError,
}: {
  tournament: Tournament;
  teams: TournamentTeam[];
  onChanged: (updated: Tournament, message: string, undo?: () => Promise<Tournament>) => void;
  onError: (text: string) => void;
}) {
  const { t, money } = useI18n();
  const [busy, setBusy] = useState(false);
  // The API's window: from an hour before the start until the end.
  // eslint-disable-next-line react/purity -- the buttons follow the clock; the page re-renders on every change
  const now = Date.now();
  const canCheckIn =
    tr.status === 'Published' && now >= new Date(tr.startsAt).getTime() - HOUR && now <= new Date(tr.endsAt).getTime();
  const fee = money(tr.entryFee, tr.currency);
  const url = (team: TournamentTeam, action: string) => `/venue/tournaments/${tr.id}/teams/${team.id}/${action}`;

  async function run(team: TournamentTeam, action: string, message: string, undoAction?: string) {
    setBusy(true);
    try {
      const updated = await api<Tournament>(url(team, action), { method: 'POST' });
      onChanged(updated, message, undoAction ? () => api<Tournament>(url(team, undoAction), { method: 'POST' }) : undefined);
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <table className="mg-table">
      <thead>
        <tr>
          <th>{t('team')}</th>
          <th>{t('captain')}</th>
          <th>{t('players')}</th>
          <th>{t('status')}</th>
          <th>
            <span className="visually-hidden">{t('actions')}</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {teams.map((team) => (
          <tr key={team.id}>
            <td>{team.name}</td>
            <td>
              {team.captain.name}{' '}
              <bdi dir="ltr" className="muted">
                {team.captain.maskedPhone}
              </bdi>
            </td>
            <td>{team.members.join(', ')}</td>
            <td>
              {team.checkedInAt ? (
                <Pill tone="success">{t('statusCheckedInAmount', { amount: money(team.cashCollected ?? tr.entryFee, tr.currency) })}</Pill>
              ) : team.status === 'Registered' ? (
                <Pill>{t('teamRegistered')}</Pill>
              ) : team.status === 'Waitlisted' ? (
                <Pill tone="warning">{t('teamWaitlisted')}</Pill>
              ) : (
                <Pill>{t('teamWithdrawn')}</Pill>
              )}
            </td>
            <td className="row-actions">
              {team.status === 'Registered' && !team.checkedInAt && canCheckIn && (
                <Button
                  disabled={busy}
                  aria-label={t('takeCashCheckInFor', { amount: fee, name: team.name })}
                  onClick={() =>
                    void run(team, 'check-in', t('checkedInNotice', { name: team.name, amount: fee }), 'undo-check-in')
                  }
                >
                  {tr.entryFee > 0 ? t('takeCashCheckIn', { amount: fee }) : t('checkIn')}
                </Button>
              )}
              {recent(team.checkedInAt) && (
                <Button
                  variant="secondary"
                  disabled={busy}
                  aria-label={t('undoCheckInFor', { name: team.name })}
                  onClick={() => void run(team, 'undo-check-in', t('undone'))}
                >
                  {t('undoCheckIn')}
                </Button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
