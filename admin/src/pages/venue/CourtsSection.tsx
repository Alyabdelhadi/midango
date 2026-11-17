import { useState, type FormEvent } from 'react';
import { Button } from '../../components/Button';
import { Dialog } from '../../components/Dialog';
import { Field } from '../../components/Field';
import { Notice } from '../../components/Notice';
import { Pill } from '../../components/Pill';
import { errorText } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import { SportIcon } from '../../lib/sportIcons';
import type { Category, Court, CourtInput, Venue } from '../../lib/types';

export function CourtsSection({
  venue,
  categories,
  canEdit,
  onSave,
}: {
  venue: Venue;
  categories: Category[];
  canEdit: boolean;
  /** Throws on failure so the dialog can show the error next to the form. */
  onSave: (courtId: string | null, input: CourtInput) => Promise<void>;
}) {
  const { t, name, money } = useI18n();
  const [editing, setEditing] = useState<Court | 'new' | null>(null);
  const categoryById = new Map(categories.map((c) => [c.id, c]));

  const featureNames = (court: Court) => {
    const category = categoryById.get(court.categoryId);
    const names = court.featureIds
      .map((id) => category?.features.find((f) => f.id === id))
      .filter((f) => f !== undefined)
      .map((f) => name(f));
    return names.join(', ') || '—';
  };

  return (
    <section className="card">
      <div className="card-head">
        <h2>{t('courts')}</h2>
        {canEdit && (
          <Button
            variant="secondary"
            onClick={() => setEditing('new')}
            disabledReason={categories.length === 0 ? t('noSportsYet') : undefined}
          >
            {t('addCourt')}
          </Button>
        )}
      </div>

      {venue.courts.length === 0 ? (
        <p className="empty">{t('noCourts')}</p>
      ) : (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('court')}</th>
              <th>{t('sport')}</th>
              <th>{t('setting')}</th>
              <th>{t('pricePerHour')}</th>
              <th>{t('courtFeatures')}</th>
              <th>{t('status')}</th>
              {canEdit && (
                <th>
                  <span className="visually-hidden">{t('actions')}</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {venue.courts.map((court) => {
              const category = categoryById.get(court.categoryId);
              return (
                <tr key={court.id}>
                  <td>{name(court)}</td>
                  <td>
                    {category ? (
                      <span className="cell-icon">
                        <SportIcon iconKey={category.iconKey} size={16} />
                        {name(category)}
                      </span>
                    ) : (
                      t('hiddenSport')
                    )}
                  </td>
                  <td>{court.indoor ? t('indoor') : t('outdoor')}</td>
                  <td className="numeric">{t('perHour', { price: money(court.hourlyPrice, venue.currencyCode) })}</td>
                  <td>{featureNames(court)}</td>
                  <td>
                    <Pill tone={court.active ? 'success' : 'warning'}>{court.active ? t('courtOpen') : t('courtClosed')}</Pill>
                  </td>
                  {canEdit && (
                    <td className="row-actions">
                      <Button variant="text" onClick={() => setEditing(court)}>
                        {t('edit')}
                      </Button>
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      )}

      <Dialog open={editing !== null} title={editing === 'new' ? t('addCourt') : t('editCourt')} onClose={() => setEditing(null)}>
        {editing !== null && (
          <CourtForm
            court={editing === 'new' ? null : editing}
            categories={categories}
            nextSortOrder={venue.courts.length + 1}
            onCancel={() => setEditing(null)}
            onSave={async (input) => {
              await onSave(editing === 'new' ? null : editing.id, input);
              setEditing(null);
            }}
          />
        )}
      </Dialog>
    </section>
  );
}

function CourtForm({
  court,
  categories,
  nextSortOrder,
  onCancel,
  onSave,
}: {
  court: Court | null;
  categories: Category[];
  nextSortOrder: number;
  onCancel: () => void;
  onSave: (input: CourtInput) => Promise<void>;
}) {
  const { t, name } = useI18n();
  const [draft, setDraft] = useState<CourtInput>(
    court
      ? {
          categoryId: court.categoryId,
          nameEn: court.nameEn,
          nameAr: court.nameAr,
          indoor: court.indoor,
          hourlyPrice: court.hourlyPrice,
          featureIds: court.featureIds,
          sortOrder: court.sortOrder,
          active: court.active,
        }
      : {
          categoryId: categories[0]?.id ?? '',
          nameEn: '',
          nameAr: '',
          indoor: false,
          hourlyPrice: 0,
          featureIds: [],
          sortOrder: nextSortOrder,
          active: true,
        },
  );
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const category = categories.find((c) => c.id === draft.categoryId);
  const set = (patch: Partial<CourtInput>) => setDraft((d) => ({ ...d, ...patch }));
  const toggleFeature = (id: string) =>
    set({ featureIds: draft.featureIds.includes(id) ? draft.featureIds.filter((x) => x !== id) : [...draft.featureIds, id] });

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await onSave(draft);
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={submit}>
      {error && <Notice tone="error">{error}</Notice>}
      <Field label={t('sport')}>
        <select required value={draft.categoryId} onChange={(e) => set({ categoryId: e.target.value, featureIds: [] })}>
          {!category && <option value={draft.categoryId}>{t('hiddenSport')}</option>}
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {name(c)}
            </option>
          ))}
        </select>
      </Field>
      <div className="form-grid">
        <Field label={t('nameEn')}>
          <input required maxLength={60} value={draft.nameEn} onChange={(e) => set({ nameEn: e.target.value })} />
        </Field>
        <Field label={t('nameAr')}>
          <input required maxLength={60} dir="rtl" lang="ar" value={draft.nameAr} onChange={(e) => set({ nameAr: e.target.value })} />
        </Field>
      </div>
      <label className="check-row">
        <input type="checkbox" checked={draft.indoor} onChange={(e) => set({ indoor: e.target.checked })} />
        {t('indoor')}
      </label>
      <Field label={t('pricePerHour')}>
        <input
          type="number"
          required
          min={1}
          max={10000}
          step="0.01"
          inputMode="decimal"
          value={draft.hourlyPrice || ''}
          onChange={(e) => set({ hourlyPrice: Number(e.target.value) })}
        />
      </Field>
      {category && category.features.length > 0 && (
        <fieldset className="mg-fieldset">
          <legend>{t('courtFeatures')}</legend>
          <div className="chip-row">
            {category.features.map((feature) => (
              <label key={feature.id} className="check-chip">
                <input type="checkbox" checked={draft.featureIds.includes(feature.id)} onChange={() => toggleFeature(feature.id)} />
                {name(feature)}
              </label>
            ))}
          </div>
        </fieldset>
      )}
      <div className="form-grid">
        <Field label={t('sortOrder')}>
          <input type="number" min={0} value={draft.sortOrder} onChange={(e) => set({ sortOrder: Number(e.target.value) })} />
        </Field>
        <label className="check-row">
          <input type="checkbox" checked={draft.active} onChange={(e) => set({ active: e.target.checked })} />
          {t('courtOpenForBooking')}
        </label>
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
