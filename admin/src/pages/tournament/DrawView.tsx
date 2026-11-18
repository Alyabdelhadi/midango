import { useI18n } from '../../lib/i18n';
import { clock } from '../../lib/time';
import type { MatchTeam, TournamentGroup, TournamentMatch } from '../../lib/types';
import { hasResult, knockoutRounds, roundLabel, sideName } from './matches';

/** Rounds as columns; each pair of matches joins into the match it feeds. Logical CSS, so it mirrors in RTL. */
export function Bracket({ matches }: { matches: TournamentMatch[] }) {
  const { t } = useI18n();
  const rounds = knockoutRounds(matches);
  return (
    <div className="bracket-scroll">
      <div className="bracket">
        {rounds.map((round, i) => {
          const pairs = Array.from({ length: Math.ceil(round.length / 2) }, (_, k) => round.slice(k * 2, k * 2 + 2));
          return (
            <section key={i} className="bracket-round">
              <h3>{roundLabel(i + 1, rounds.length, t)}</h3>
              <div className="bracket-body">
                {pairs.map((pair) => (
                  <div key={pair[0].id} className={pair.length === 2 ? 'bracket-pair bracket-pair--joined' : 'bracket-pair'}>
                    {pair.map((m) => (
                      <div key={m.id} className="bracket-slot">
                        <BracketMatch match={m} />
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
}

function BracketMatch({ match: m }: { match: TournamentMatch }) {
  const { t, name } = useI18n();
  const played = hasResult(m);
  const side = (team: MatchTeam | null, score: number | null) => {
    const won = team !== null && team.id === m.winnerTeamId;
    return (
      <div className={won ? 'bracket-side bracket-side--winner' : 'bracket-side'}>
        <span className={team ? undefined : 'muted'}>
          {sideName(team, t)}
          {won && <span className="visually-hidden"> ({t('winner')})</span>}
        </span>
        {played && <span className="numeric">{score}</span>}
      </div>
    );
  };
  return (
    <div className="bracket-match">
      {side(m.teamA, m.scoreA)}
      {m.isBye ? (
        <div className="bracket-side muted">{t('bye')}</div>
      ) : (
        side(m.teamB, m.scoreB)
      )}
      {!m.isBye && m.startsAt && (
        <div className="bracket-meta">
          <bdi dir="ltr">{clock(m.startsAt)}</bdi>
          {m.courtNameEn && <span> · {name({ nameEn: m.courtNameEn, nameAr: m.courtNameAr ?? m.courtNameEn })}</span>}
        </div>
      )}
    </div>
  );
}

/** One standings table per group, in the order the API ranks them. */
export function GroupTables({ groups }: { groups: TournamentGroup[] }) {
  const { t } = useI18n();
  return groups.map((g) => (
    <table key={g.groupNo} className="mg-table">
      {groups.length > 1 && <caption className="table-caption">{t('groupN', { n: g.groupNo })}</caption>}
      <thead>
        <tr>
          <th scope="col" className="numeric">
            {t('standingRank')}
          </th>
          <th scope="col">{t('team')}</th>
          <th scope="col" className="numeric">
            {t('played')}
          </th>
          <th scope="col" className="numeric">
            {t('won')}
          </th>
          <th scope="col" className="numeric">
            {t('drawn')}
          </th>
          <th scope="col" className="numeric">
            {t('lost')}
          </th>
          <th scope="col" className="numeric">
            {t('scoreForCol')}
          </th>
          <th scope="col" className="numeric">
            {t('scoreAgainstCol')}
          </th>
          <th scope="col" className="numeric">
            {t('points')}
          </th>
        </tr>
      </thead>
      <tbody>
        {g.standings.map((s, i) => (
          <tr key={s.teamId}>
            <td className="numeric">{i + 1}</td>
            <th scope="row">{s.name}</th>
            <td className="numeric">{s.played}</td>
            <td className="numeric">{s.won}</td>
            <td className="numeric">{s.drawn}</td>
            <td className="numeric">{s.lost}</td>
            <td className="numeric">{s.scoreFor}</td>
            <td className="numeric">{s.scoreAgainst}</td>
            <td className="numeric">
              <strong>{s.points}</strong>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  ));
}
