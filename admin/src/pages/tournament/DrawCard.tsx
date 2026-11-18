import { useState } from 'react';
import { Button } from '../../components/Button';
import { Field } from '../../components/Field';
import { api, errorText } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import { clock, dayOf, formatDay } from '../../lib/time';
import type { Tournament } from '../../lib/types';
import { hasResult } from './matches';

/** The manager's draw: match length, groups (round robin), Draw / Redraw. Locked once a result exists. */
export function DrawCard({
  tournament: tr,
  onDrawn,
  onError,
}: {
  tournament: Tournament;
  onDrawn: (updated: Tournament) => void;
  onError: (text: string) => void;
}) {
  const { t, lang } = useI18n();
  const registered = tr.teams.filter((x) => x.status === 'Registered').length;
  const maxGroups = Math.max(1, Math.floor(registered / 2));
  const [minutes, setMinutes] = useState(tr.matchMinutes ?? 60);
  const [groups, setGroups] = useState(tr.groupCount ?? (registered <= 6 ? 1 : Math.ceil(registered / 4)));
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const when = (iso: string) => `${formatDay(dayOf(iso), lang, 'short')} ${clock(iso)}`;
  const locked = tr.matches.some(hasResult);
  // eslint-disable-next-line react/purity -- the button follows the clock; the page re-renders on every change
  const registrationOpen = Date.now() < new Date(tr.registrationClosesAt).getTime();
  const reason = registrationOpen
    ? t('drawWaitsForClose', { when: when(tr.registrationClosesAt) })
    : registered < 2
      ? t('drawNeedsTeams')
      : !(minutes >= 15 && minutes <= 240)
        ? t('matchMinutesHint')
        : undefined;

  async function draw() {
    setBusy(true);
    try {
      const body = { matchMinutes: minutes, groupCount: tr.format === 'RoundRobin' ? Math.min(groups, maxGroups) : null };
      onDrawn(await api<Tournament>(`/venue/tournaments/${tr.id}/draw`, { method: 'POST', body }));
      setConfirming(false);
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card">
      <h2>{t('drawTitle')}</h2>
      {tr.drawnAt && tr.matchMinutes && (
        <p className="muted">{t('drawnAtLine', { when: when(tr.drawnAt), minutes: tr.matchMinutes })}</p>
      )}
      {locked ? (
        <p className="muted">{t('drawLocked')}</p>
      ) : (
        <>
          <p className="muted">{t('drawHint')}</p>
          <div className="form-grid">
            <Field label={t('matchMinutes')} hint={t('matchMinutesHint')}>
              <input
                type="number"
                min={15}
                max={240}
                step={5}
                inputMode="numeric"
                value={minutes}
                onChange={(e) => setMinutes(e.target.valueAsNumber)}
              />
            </Field>
            {tr.format === 'RoundRobin' && (
              <Field label={t('groupCount')} hint={t('groupCountHint')}>
                <select value={Math.min(groups, maxGroups)} onChange={(e) => setGroups(Number(e.target.value))}>
                  {Array.from({ length: maxGroups }, (_, i) => (
                    <option key={i + 1} value={i + 1}>
                      {i + 1}
                    </option>
                  ))}
                </select>
              </Field>
            )}
          </div>
          {confirming ? (
            <div className="panel-section">
              <p>{t('confirmRedraw')}</p>
              <div className="form-actions">
                <Button variant="secondary" onClick={() => setConfirming(false)}>
                  {t('keepIt')}
                </Button>
                <Button disabled={busy} disabledReason={reason} onClick={() => void draw()}>
                  {t('yesRedraw')}
                </Button>
              </div>
            </div>
          ) : (
            <div>
              {tr.drawnAt ? (
                <Button variant="secondary" disabledReason={reason} onClick={() => setConfirming(true)}>
                  {t('redraw')}
                </Button>
              ) : (
                <Button disabled={busy} disabledReason={reason} onClick={() => void draw()}>
                  {t('makeDraw')}
                </Button>
              )}
            </div>
          )}
        </>
      )}
    </section>
  );
}
