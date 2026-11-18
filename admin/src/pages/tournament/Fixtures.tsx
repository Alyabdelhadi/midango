import { useState, type FormEvent } from 'react';
import { Button } from '../../components/Button';
import { Dialog } from '../../components/Dialog';
import { Field } from '../../components/Field';
import { Notice } from '../../components/Notice';
import { api, errorText } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import { clock, dayOf, formatDay, fromLocalInput, offsetOf, toLocalInput } from '../../lib/time';
import type { MatchTeam, Tournament, TournamentMatch } from '../../lib/types';
import { groupFixtures, hasResult, knockoutRounds, roundLabel, sideName } from './matches';

type Props = {
  tournament: Tournament;
  onChanged: (updated: Tournament, message: string) => void;
  onError: (text: string) => void;
};

/** Every match by round (knockout) or group (round robin): time, court, teams, score entry, reschedule. */
export function Fixtures({ tournament: tr, onChanged, onError }: Props) {
  const { t } = useI18n();
  const [moving, setMoving] = useState<TournamentMatch>();
  const rounds = knockoutRounds(tr.matches);
  const sections: [string, TournamentMatch[]][] =
    tr.format === 'Knockout'
      ? rounds.map((r, i) => [roundLabel(i + 1, rounds.length, t), r])
      : groupFixtures(tr.matches).map(([g, ms]) => [t('groupN', { n: g }), ms]);

  return (
    <>
      {sections.map(([label, matches]) => (
        <table key={label} className="mg-table fixtures-table">
          <caption className="table-caption">{label}</caption>
          <thead>
            <tr>
              <th scope="col">{t('time')}</th>
              <th scope="col">{t('court')}</th>
              <th scope="col">{t('match')}</th>
              <th scope="col">{t('score')}</th>
              <th scope="col">
                <span className="visually-hidden">{t('actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {matches.map((m) => (
              <FixtureRow
                key={`${m.id}|${m.scoreA}|${m.scoreB}`}
                tournament={tr}
                match={m}
                onChanged={onChanged}
                onError={onError}
                onMove={() => setMoving(m)}
              />
            ))}
          </tbody>
        </table>
      ))}
      <Dialog
        open={moving !== undefined}
        title={moving ? t('rescheduleTitle', { a: sideName(moving.teamA, t), b: sideName(moving.teamB, t) }) : ''}
        onClose={() => setMoving(undefined)}
      >
        {moving && (
          <RescheduleForm
            tournament={tr}
            match={moving}
            onCancel={() => setMoving(undefined)}
            onSaved={(updated) => {
              setMoving(undefined);
              onChanged(updated, t('rescheduledNotice'));
            }}
          />
        )}
      </Dialog>
    </>
  );
}

function FixtureRow({
  tournament: tr,
  match: m,
  onChanged,
  onError,
  onMove,
}: Omit<Props, 'tournament'> & { tournament: Tournament; match: TournamentMatch; onMove: () => void }) {
  const { t, lang, name } = useI18n();
  const [a, setA] = useState(m.scoreA?.toString() ?? '');
  const [b, setB] = useState(m.scoreB?.toString() ?? '');
  const [busy, setBusy] = useState(false);
  const played = hasResult(m);
  const live = tr.status === 'InProgress' || tr.status === 'Finished';
  const playable = live && !m.isBye && m.teamA !== null && m.teamB !== null;
  // A knockout result is fixed once the match it feeds has one.
  const next = m.nextMatchId ? tr.matches.find((x) => x.id === m.nextMatchId) : undefined;
  const nextPlayed = m.stage === 'Knockout' && next !== undefined && hasResult(next);
  const editable = playable && !nextPlayed;
  const names = { a: sideName(m.teamA, t), b: sideName(m.teamB, t) };
  const multiDay = dayOf(tr.startsAt) !== dayOf(tr.endsAt);
  const url = `/venue/tournaments/${tr.id}/matches/${m.id}/result`;

  async function send(method: 'PUT' | 'DELETE', message: string) {
    const scoreA = Number(a);
    const scoreB = Number(b);
    if (method === 'PUT' && !(valid(a) && valid(b))) return onError(t('scoresNeeded'));
    setBusy(true);
    try {
      onChanged(await api<Tournament>(url, { method, body: method === 'PUT' ? { scoreA, scoreB } : undefined }), message);
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  const side = (team: MatchTeam | null) => {
    const won = team !== null && team.id === m.winnerTeamId;
    return (
      <span className={won ? 'match-winner' : team ? undefined : 'muted'}>
        {sideName(team, t)}
        {won && <span className="visually-hidden"> ({t('winner')})</span>}
      </span>
    );
  };

  return (
    <tr>
      <td className="nowrap">
        {m.startsAt ? (
          <>
            {multiDay && `${formatDay(dayOf(m.startsAt), lang, 'short')} `}
            <bdi dir="ltr">{clock(m.startsAt)}</bdi>
          </>
        ) : (
          <span className="muted">{m.isBye ? '—' : t('notScheduled')}</span>
        )}
      </td>
      <td>{m.courtNameEn ? name({ nameEn: m.courtNameEn, nameAr: m.courtNameAr ?? m.courtNameEn }) : '—'}</td>
      <td>
        {m.isBye ? (
          <span className="muted">{t('byeFor', { name: names.a })}</span>
        ) : (
          <>
            {side(m.teamA)} <span className="muted">{t('versus')}</span> {side(m.teamB)}
          </>
        )}
      </td>
      <td>
        {editable ? (
          <span className="score-inputs">
            <input
              type="number"
              min={0}
              max={99}
              step={1}
              inputMode="numeric"
              aria-label={t('scoreForTeam', { name: names.a })}
              value={a}
              onChange={(e) => setA(e.target.value)}
            />
            <span aria-hidden="true">–</span>
            <input
              type="number"
              min={0}
              max={99}
              step={1}
              inputMode="numeric"
              aria-label={t('scoreForTeam', { name: names.b })}
              value={b}
              onChange={(e) => setB(e.target.value)}
            />
          </span>
        ) : played ? (
          <span className="numeric">
            {m.scoreA} – {m.scoreB}
          </span>
        ) : null}
      </td>
      <td className="row-actions">
        {editable && (
          <Button
            disabled={busy}
            disabledReason={m.stage === 'Knockout' && a !== '' && a === b ? t('noTieKnockout') : undefined}
            aria-label={t('saveResultFor', names)}
            onClick={() => void send('PUT', t('resultSavedNotice'))}
          >
            {t('save')}
          </Button>
        )}
        {editable && played && (
          <Button
            variant="secondary"
            disabled={busy}
            aria-label={t('clearResultFor', names)}
            onClick={() => void send('DELETE', t('resultClearedNotice'))}
          >
            {t('clearResult')}
          </Button>
        )}
        {playable && nextPlayed && <span className="muted">{t('nextMatchPlayed')}</span>}
        {tr.status === 'InProgress' && !m.isBye && (
          <Button variant="text" aria-label={t('rescheduleFor', names)} onClick={onMove}>
            {t('reschedule')}
          </Button>
        )}
      </td>
    </tr>
  );
}

const valid = (s: string) => /^\d{1,2}$/.test(s);

function RescheduleForm({
  tournament: tr,
  match: m,
  onCancel,
  onSaved,
}: {
  tournament: Tournament;
  match: TournamentMatch;
  onCancel: () => void;
  onSaved: (updated: Tournament) => void;
}) {
  const { t, name } = useI18n();
  const [courtId, setCourtId] = useState(m.courtId ?? tr.courts[0]?.id ?? '');
  const [startsAt, setStartsAt] = useState(toLocalInput(m.startsAt ?? tr.startsAt));
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      const body = { courtId, startsAt: fromLocalInput(startsAt, offsetOf(tr.startsAt)) };
      onSaved(await api<Tournament>(`/venue/tournaments/${tr.id}/matches/${m.id}/schedule`, { method: 'PUT', body }));
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={submit}>
      <div className="form-grid">
        <Field label={t('court')}>
          <select value={courtId} onChange={(e) => setCourtId(e.target.value)}>
            {tr.courts.map((c) => (
              <option key={c.id} value={c.id}>
                {name(c)}
              </option>
            ))}
          </select>
        </Field>
        <Field label={t('startsAt')}>
          <input
            type="datetime-local"
            required
            step={300}
            min={toLocalInput(tr.startsAt)}
            max={toLocalInput(tr.endsAt)}
            value={startsAt}
            onChange={(e) => setStartsAt(e.target.value)}
          />
        </Field>
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="form-actions">
        <Button variant="secondary" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t('save')}
        </Button>
      </div>
    </form>
  );
}
