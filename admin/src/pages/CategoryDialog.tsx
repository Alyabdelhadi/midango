import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { UploadButton } from '../components/UploadButton';
import { api, errorText, fileBody } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { SPORT_ICON_KEYS, SportIcon } from '../lib/sportIcons';
import type { Category } from '../lib/types';

type FeatureDraft = { id?: string; nameEn: string; nameAr: string; iconUrl?: string | null };

const DURATIONS = [30, 60, 90, 120, 150, 180, 210, 240];

export function CategoryDialog({
  category,
  nextSortOrder,
  onClose,
  onSaved,
  onIconChanged,
}: {
  category: Category | 'new' | null;
  nextSortOrder: number;
  onClose: () => void;
  onSaved: () => void;
  /** Icons are saved the moment they're uploaded, so the list behind the dialog refreshes then too. */
  onIconChanged: () => void;
}) {
  const { t } = useI18n();
  return (
    <Dialog open={category !== null} title={category === 'new' ? t('newCategory') : t('editCategory')} onClose={onClose}>
      {category !== null && (
        <CategoryForm
          initial={category === 'new' ? undefined : category}
          nextSortOrder={nextSortOrder}
          onCancel={onClose}
          onSaved={onSaved}
          onIconChanged={onIconChanged}
        />
      )}
    </Dialog>
  );
}

function CategoryForm({
  initial,
  nextSortOrder,
  onCancel,
  onSaved,
  onIconChanged,
}: {
  initial?: Category;
  nextSortOrder: number;
  onCancel: () => void;
  onSaved: () => void;
  onIconChanged: () => void;
}) {
  const { t } = useI18n();
  const [nameEn, setNameEn] = useState(initial?.nameEn ?? '');
  const [nameAr, setNameAr] = useState(initial?.nameAr ?? '');
  const [iconKey, setIconKey] = useState(initial?.iconKey ?? SPORT_ICON_KEYS[0]);
  const [durations, setDurations] = useState<number[]>(initial?.durationsMinutes ?? [60, 90]);
  const [sortOrder, setSortOrder] = useState(initial?.sortOrder ?? nextSortOrder);
  const [active, setActive] = useState(initial?.active ?? true);
  const [features, setFeatures] = useState<FeatureDraft[]>(initial?.features ?? []);
  const [iconUrl, setIconUrl] = useState(initial?.iconUrl ?? null);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  /** Uploads (file) or removes (no file) an icon at `path`; keeps unsaved edits, takes only the icon urls back. */
  async function changeIcon(path: string, file?: File) {
    setBusy(true);
    setError(undefined);
    try {
      const updated = await api<Category>(path, file ? { method: 'PUT', body: fileBody(file) } : { method: 'DELETE' });
      setIconUrl(updated.iconUrl);
      setFeatures((list) =>
        list.map((f) => {
          const saved = updated.features.find((u) => u.id === f.id);
          return saved ? { ...f, iconUrl: saved.iconUrl } : f;
        }),
      );
      onIconChanged();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  const toggleDuration = (minutes: number) =>
    setDurations((d) => (d.includes(minutes) ? d.filter((x) => x !== minutes) : [...d, minutes].sort((a, b) => a - b)));

  const updateFeature = (index: number, patch: Partial<FeatureDraft>) =>
    setFeatures((list) => list.map((f, n) => (n === index ? { ...f, ...patch } : f)));

  const moveFeature = (index: number, by: -1 | 1) =>
    setFeatures((list) => {
      const target = index + by;
      if (target < 0 || target >= list.length) return list;
      const next = [...list];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (durations.length < 1 || durations.length > 4) {
      setError(t('durationsRule'));
      return;
    }
    setBusy(true);
    setError(undefined);
    const body = {
      nameEn,
      nameAr,
      iconKey,
      durationsMinutes: durations,
      sortOrder,
      active,
      features: features.map(({ id, nameEn, nameAr }) => ({ id, nameEn, nameAr })),
    };
    try {
      if (initial) await api(`/admin/categories/${initial.id}`, { method: 'PUT', body });
      else await api('/admin/categories', { method: 'POST', body });
      onSaved();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={submit}>
      {error && <Notice tone="error">{error}</Notice>}

      <div className="form-grid">
        <Field label={t('nameEn')}>
          <input required maxLength={60} value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
        </Field>
        <Field label={t('nameAr')}>
          <input required maxLength={60} dir="rtl" lang="ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
        </Field>
      </div>

      <fieldset className="mg-fieldset">
        <legend>{t('icon')}</legend>
        <div className="icon-grid">
          {SPORT_ICON_KEYS.map((key) => (
            <label key={key} className="icon-option" title={key}>
              <input type="radio" name="icon" value={key} checked={iconKey === key} onChange={() => setIconKey(key)} />
              <SportIcon iconKey={key} size={22} />
              <span className="visually-hidden">{key}</span>
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset className="mg-fieldset">
        <legend>{t('uploadedIcon')}</legend>
        {initial ? (
          <div className="chip-row">
            {iconUrl && <SportIcon iconKey={iconKey} iconUrl={iconUrl} size={32} />}
            <UploadButton disabled={busy} onFiles={([file]) => void changeIcon(`/admin/categories/${initial.id}/icon`, file)}>
              {t('uploadIcon')}
            </UploadButton>
            {iconUrl && (
              <Button variant="text" disabled={busy} onClick={() => void changeIcon(`/admin/categories/${initial.id}/icon`)}>
                {t('remove')}
              </Button>
            )}
          </div>
        ) : null}
        <span className="mg-field-hint">{initial ? t('uploadedIconHint') : t('saveFirstForIcon')}</span>
      </fieldset>

      <fieldset className="mg-fieldset">
        <legend>{t('bookingLengths')}</legend>
        <div className="chip-row">
          {DURATIONS.map((minutes) => (
            <label key={minutes} className="check-chip">
              <input type="checkbox" checked={durations.includes(minutes)} onChange={() => toggleDuration(minutes)} />
              {t('minutesShort', { m: minutes })}
            </label>
          ))}
        </div>
        <span className="mg-field-hint">{t('durationsRule')}</span>
      </fieldset>

      <fieldset className="mg-fieldset">
        <legend>{t('courtFeatures')}</legend>
        {features.map((feature, index) => (
          <div key={feature.id ?? `new-${index}`} className="feature-row">
            <input
              aria-label={t('nameEn')}
              placeholder={t('nameEn')}
              required
              maxLength={60}
              value={feature.nameEn}
              onChange={(e) => updateFeature(index, { nameEn: e.target.value })}
            />
            <input
              aria-label={t('nameAr')}
              placeholder={t('nameAr')}
              required
              maxLength={60}
              dir="rtl"
              lang="ar"
              value={feature.nameAr}
              onChange={(e) => updateFeature(index, { nameAr: e.target.value })}
            />
            <Button variant="text" aria-label={t('moveUp')} disabled={index === 0} onClick={() => moveFeature(index, -1)}>
              ↑
            </Button>
            <Button
              variant="text"
              aria-label={t('moveDown')}
              disabled={index === features.length - 1}
              onClick={() => moveFeature(index, 1)}
            >
              ↓
            </Button>
            <Button variant="text" onClick={() => setFeatures((list) => list.filter((_, n) => n !== index))}>
              {t('remove')}
            </Button>
            {feature.id ? (
              <FeatureIcon
                iconUrl={feature.iconUrl ?? null}
                label={feature.nameEn || feature.nameAr}
                busy={busy}
                onChange={(file) => void changeIcon(`/admin/features/${feature.id}/icon`, file)}
              />
            ) : (
              <span />
            )}
          </div>
        ))}
        {features.some((f) => !f.id) && <span className="mg-field-hint">{t('saveFirstForIcon')}</span>}
        <div>
          <Button variant="secondary" onClick={() => setFeatures((list) => [...list, { nameEn: '', nameAr: '' }])}>
            {t('addFeature')}
          </Button>
        </div>
      </fieldset>

      <div className="form-grid">
        <Field label={t('sortOrder')}>
          <input type="number" min={0} value={sortOrder} onChange={(e) => setSortOrder(Number(e.target.value))} />
        </Field>
        <label className="check-row">
          <input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} />
          {t('showInApp')}
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

/** A saved feature's icon: a small preview with Remove, or an upload button. */
function FeatureIcon({
  iconUrl,
  label,
  busy,
  onChange,
}: {
  iconUrl: string | null;
  label: string;
  busy: boolean;
  onChange: (file?: File) => void;
}) {
  const { t } = useI18n();
  return iconUrl ? (
    <span className="cell-icon">
      <SportIcon iconKey="" iconUrl={iconUrl} size={20} />
      <Button variant="text" disabled={busy} aria-label={t('removeIconFor', { name: label })} onClick={() => onChange()}>
        {t('removeIcon')}
      </Button>
    </span>
  ) : (
    <UploadButton
      variant="text"
      disabled={busy}
      aria-label={t('uploadIconFor', { name: label })}
      onFiles={([file]) => onChange(file)}
    >
      {t('uploadIcon')}
    </UploadButton>
  );
}
