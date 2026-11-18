import type { MatchTeam, TournamentMatch } from '../../lib/types';
import type { MessageKey } from '../../locales/en';

type T = (key: MessageKey, vars?: Record<string, string | number>) => string;

export const hasResult = (m: TournamentMatch) => m.scoreA !== null && m.scoreB !== null;

export const sideName = (team: MatchTeam | null, t: T) => team?.name ?? t('toBeDecided');

/** "Final", "Semi-finals", "Quarter-finals", else "Round n". */
export function roundLabel(round: number, lastRound: number, t: T): string {
  if (round === lastRound) return t('roundFinal');
  if (round === lastRound - 1) return t('roundSemis');
  if (round === lastRound - 2) return t('roundQuarters');
  return t('roundN', { n: round });
}

/** Knockout matches as rounds (1, 2, …), each in bracket order. */
export function knockoutRounds(matches: TournamentMatch[]): TournamentMatch[][] {
  const ko = matches.filter((m) => m.stage === 'Knockout');
  const last = Math.max(0, ...ko.map((m) => m.round));
  return Array.from({ length: last }, (_, i) =>
    ko.filter((m) => m.round === i + 1).sort((a, b) => a.position - b.position),
  );
}

/** Group matches per group, in playing order. */
export function groupFixtures(matches: TournamentMatch[]): [number, TournamentMatch[]][] {
  const groups = [...new Set(matches.filter((m) => m.stage === 'Group').map((m) => m.groupNo ?? 1))].sort((a, b) => a - b);
  return groups.map((g) => [
    g,
    matches
      .filter((m) => m.stage === 'Group' && (m.groupNo ?? 1) === g)
      .sort((a, b) => a.round - b.round || (a.startsAt ?? '').localeCompare(b.startsAt ?? '') || a.position - b.position),
  ]);
}
