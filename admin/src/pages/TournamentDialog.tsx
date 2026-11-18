import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { fromLocalInput, offsetOf, toLocalInput } from '../lib/time';
import {
  FORMAT_KEY,
  LEVEL_KEY,
  type Category,
  type PlayLevel,
  type Today,
  type Tournament,
  type TournamentFormat,
  type TournamentInput,
  type Venue,
} from '../lib/types';
import { useLoad } from '../lib/useLoad';

const FORMATS: TournamentFormat[] = ['Knockout', 'RoundRobin'];
const LEVELS: PlayLevel[] = ['Beginner', 'Intermediate', 'Advanced'];

const defaultTeamSize = (c?: Category) => Math.max(1, Math.floor((c?.playersPerMatch ?? 2) / 2));

/** Create (initial undefined) or edit a tournament. Once published, only names, descriptions,
 *  most teams and the registration close stay editable; the rest is shown read-only. */
export function TournamentDialog({
  open,
  initial,
  onClose,
  onSaved,
}: {
  open: boolean;
  initial?: Tournament;
  onClose: () => void;
  onSaved: (t: Tournament) => void;
}) {
  const { t } = useI18n();
  return (
    <Dialog open={open} title={initial ? t('editTournament') : t('newTournament')} onClose={onClose}>
      <TournamentFormLoader initial={initial} onCancel={onClose} onSaved={onSaved} />
    </Dialog>
  );
}

function TournamentFormLoader({
  initial,
  onCancel,
  onSaved,
}: {
  initial?: Tournament;
  onCancel: () => void;
  onSaved: (t: Tournament) => void;
}) {
  const { t } = useI18n();
  const venue = useLoad(() => api<Venue>('/venue'));
  const categories = useLoad(() => api<Category[]>('/categories'));
  // A new tournament takes the venue's offset from today's opening period.
  const today = useLoad(() => api<Today>('/venue/today'));
  const error = venue.error ?? categories.error ?? today.error;
  if (error) return <Notice tone="error">{error}</Notice>;
  if (!venue.data || !categories.data || !today.data) return <p className="page-loading">{t('loading')}</p>;
  return (
    <TournamentForm
      initial={initial}
      venue={venue.data}
      categories={categories.data}
      offset={initial ? offsetOf(initial.startsAt) : offsetOf(today.data.from)}
      onCancel={onCancel}
      onSaved={onSaved}
    />
  );
}

function TournamentForm({
  initial,
  venue,
  categories,
  offset,
  onCancel,
  onSaved,
}: {
  initial?: Tournament;
  venue: Venue;
  categories: Category[];
  offset: string;
  onCancel: () => void;
  onSaved: (t: Tournament) => void;
}) {
  const { t, name } = useI18n();
  const courts = venue.courts.filter((c) => c.active);
  // Sports this venue has open courts for (plus the tournament's own, should its courts have closed since).
  const sports = categories.filter((c) => c.id === initial?.categoryId || courts.some((court) => court.categoryId === c.id));
  const locked = initial !== undefined && initial.status !== 'Draft';

  const [categoryId, setCategoryId] = useState(initial?.categoryId ?? sports[0]?.id ?? '');
  const [nameEn, setNameEn] = useState(initial?.nameEn ?? '');
  const [nameAr, setNameAr] = useState(initial?.nameAr ?? '');
  const [descriptionEn, setDescriptionEn] = useState(initial?.descriptionEn ?? '');
  const [descriptionAr, setDescriptionAr] = useState(initial?.descriptionAr ?? '');
  const [format, setFormat] = useState<TournamentFormat>(initial?.format ?? 'Knockout');
  const [teamSize, setTeamSize] = useState(initial?.teamSize ?? defaultTeamSize(sports[0]));
  const [maxTeams, setMaxTeams] = useState(initial?.maxTeams ?? 8);
  const [entryFee, setEntryFee] = useState(initial?.entryFee ?? 0);
  const [level, setLevel] = useState<PlayLevel | ''>(initial?.level ?? '');
  const [courtIds, setCourtIds] = useState<string[]>(initial?.courtIds ?? []);
  const [startsAt, setStartsAt] = useState(initial ? toLocalInput(initial.startsAt) : '');
  const [endsAt, setEndsAt] = useState(initial ? toLocalInput(initial.endsAt) : '');
  const [closesAt, setClosesAt] = useState(initial ? toLocalInput(initial.registrationClosesAt) : '');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const sportCourts = courts.filter((c) => c.categoryId === categoryId);

  function changeSport(id: string) {
    setCategoryId(id);
    setTeamSize(defaultTeamSize(categories.find((c) => c.id === id)));
    setCourtIds([]);
  }

  const toggleCourt = (id: string) =>
    setCourtIds((list) => (list.includes(id) ? list.filter((x) => x !== id) : [...list, id]));

  async function submit(e: FormEvent) {
    e.preventDefault();
    // "YYYY-MM-DDTHH:mm" strings in the same offset compare correctly as text.
    if (courtIds.length === 0) return setError(t('pickCourts'));
    if (endsAt <= startsAt) return setError(t('endsBeforeStart'));
    if (closesAt > startsAt) return setError(t('closesAfterStart'));
    setBusy(true);
    setError(undefined);
    const body: TournamentInput = {
      categoryId,
      nameEn: nameEn.trim(),
      nameAr: nameAr.trim(),
      descriptionEn: descriptionEn.trim() || null,
      descriptionAr: descriptionAr.trim() || null,
      format,
      teamSize,
      maxTeams,
      entryFee,
      level: level || null,
      courtIds,
      startsAt: fromLocalInput(startsAt, offset),
      endsAt: fromLocalInput(endsAt, offset),
      registrationClosesAt: fromLocalInput(closesAt, offset),
    };
    try {
      onSaved(
        initial
          ? await api<Tournament>(`/venue/tournaments/${initial.id}`, { method: 'PUT', body })
          : await api<Tournament>('/venue/tournaments', { method: 'POST', body }),
      );
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={submit}>
      {error && <Notice tone="error">{error}</Notice>}
      {locked && <Notice tone="info">{t('lockedAfterPublish')}</Notice>}

      <div className="form-grid">
        <Field label={t('nameEn')}>
          <input required maxLength={120} value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
        </Field>
        <Field label={t('nameAr')}>
          <input required maxLength={120} dir="rtl" lang="ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
        </Field>
        <Field label={t('descriptionEn')}>
          <textarea maxLength={2000} rows={3} value={descriptionEn} onChange={(e) => setDescriptionEn(e.target.value)} />
        </Field>
        <Field label={t('descriptionAr')}>
          <textarea
            maxLength={2000}
            rows={3}
            dir="rtl"
            lang="ar"
            value={descriptionAr}
            onChange={(e) => setDescriptionAr(e.target.value)}
          />
        </Field>
      </div>

      <div className="form-grid">
        <Field label={t('sport')}>
          <select required disabled={locked} value={categoryId} onChange={(e) => changeSport(e.target.value)}>
            {sports.map((c) => (
              <option key={c.id} value={c.id}>
                {name(c)}
              </option>
            ))}
          </select>
        </Field>
        <Field label={t('level')}>
          <select disabled={locked} value={level} onChange={(e) => setLevel(e.target.value as PlayLevel | '')}>
            <option value="">{t('levelAny')}</option>
            {LEVELS.map((l) => (
              <option key={l} value={l}>
                {t(LEVEL_KEY[l])}
              </option>
            ))}
          </select>
        </Field>
      </div>

      <fieldset className="mg-fieldset" disabled={locked}>
        <legend>{t('format')}</legend>
        {FORMATS.map((f) => (
          <label key={f} className="check-row">
            <input type="radio" name="format" checked={format === f} onChange={() => setFormat(f)} />
            <span>
              <strong>{t(FORMAT_KEY[f])}</strong>
              <span className="mg-field-hint"> — {t(f === 'Knockout' ? 'formatKnockoutHint' : 'formatRoundRobinHint')}</span>
            </span>
          </label>
        ))}
      </fieldset>

      <fieldset className="mg-fieldset" disabled={locked}>
        <legend>{t('courts')}</legend>
        {sportCourts.length === 0 ? (
          <p className="empty">{t('noCourtsForSport')}</p>
        ) : (
          <div className="chip-row">
            {sportCourts.map((c) => (
              <label key={c.id} className="check-chip">
                <input type="checkbox" checked={courtIds.includes(c.id)} onChange={() => toggleCourt(c.id)} />
                {name(c)}
              </label>
            ))}
          </div>
        )}
      </fieldset>

      <div className="form-grid">
        <Field label={t('teamSize')} hint={t('teamSizeHint')}>
          <input
            type="number"
            required
            disabled={locked}
            min={1}
            max={11}
            value={teamSize}
            onChange={(e) => setTeamSize(Number(e.target.value))}
          />
        </Field>
        <Field label={t('maxTeams')} hint={t('maxTeamsHint')}>
          <input type="number" required min={2} max={64} value={maxTeams} onChange={(e) => setMaxTeams(Number(e.target.value))} />
        </Field>
        <Field label={t('entryFee')} hint={t('entryFeeHint', { currency: venue.currencyCode })}>
          <input
            type="number"
            required
            disabled={locked}
            min={0}
            max={10000}
            step={0.01}
            value={entryFee}
            onChange={(e) => setEntryFee(Number(e.target.value))}
          />
        </Field>
      </div>

      <div className="form-grid">
        <Field label={t('startsAt')} hint={t('venueTimeHint')}>
          <input type="datetime-local" required disabled={locked} value={startsAt} onChange={(e) => setStartsAt(e.target.value)} />
        </Field>
        <Field label={t('endsAt')} hint={t('venueTimeHint')}>
          <input type="datetime-local" required disabled={locked} value={endsAt} onChange={(e) => setEndsAt(e.target.value)} />
        </Field>
        <Field label={t('registrationCloses')} hint={t('closesAfterStart')}>
          <input type="datetime-local" required value={closesAt} onChange={(e) => setClosesAt(e.target.value)} />
        </Field>
      </div>

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
