import { useState, type FormEvent } from 'react';
import { Button } from '../../components/Button';
import { Field } from '../../components/Field';
import { useI18n } from '../../lib/i18n';
import type { Venue } from '../../lib/types';

export type DetailsInput = {
  nameEn: string;
  nameAr: string;
  addressEn: string;
  addressAr: string;
  latitude: number | null;
  longitude: number | null;
  timeZoneId: string;
  freeCancellationHours: number;
};

export function DetailsSection({
  venue,
  canEdit,
  onSave,
}: {
  venue: Venue;
  canEdit: boolean;
  onSave: (input: DetailsInput) => Promise<void>;
}) {
  const { t } = useI18n();
  const [draft, setDraft] = useState({
    nameEn: venue.nameEn,
    nameAr: venue.nameAr,
    addressEn: venue.addressEn ?? '',
    addressAr: venue.addressAr ?? '',
    latitude: venue.latitude?.toString() ?? '',
    longitude: venue.longitude?.toString() ?? '',
    freeCancellationHours: String(venue.freeCancellationHours),
  });
  const set = (key: keyof typeof draft) => (value: string) => setDraft((d) => ({ ...d, [key]: value }));
  const toNumber = (value: string) => (value.trim() === '' ? null : Number(value));

  function submit(e: FormEvent) {
    e.preventDefault();
    void onSave({
      nameEn: draft.nameEn,
      nameAr: draft.nameAr,
      addressEn: draft.addressEn,
      addressAr: draft.addressAr,
      latitude: toNumber(draft.latitude),
      longitude: toNumber(draft.longitude),
      timeZoneId: venue.timeZoneId,
      freeCancellationHours: Number(draft.freeCancellationHours),
    });
  }

  return (
    <section className="card">
      <h2>{t('venueDetails')}</h2>
      <form className="dialog-form" onSubmit={submit}>
        <fieldset className="form-grid" disabled={!canEdit}>
          <Field label={t('nameEn')}>
            <input required maxLength={120} value={draft.nameEn} onChange={(e) => set('nameEn')(e.target.value)} />
          </Field>
          <Field label={t('nameAr')}>
            <input required maxLength={120} dir="rtl" lang="ar" value={draft.nameAr} onChange={(e) => set('nameAr')(e.target.value)} />
          </Field>
          <Field label={t('addressEn')}>
            <input maxLength={200} value={draft.addressEn} onChange={(e) => set('addressEn')(e.target.value)} />
          </Field>
          <Field label={t('addressAr')}>
            <input maxLength={200} dir="rtl" lang="ar" value={draft.addressAr} onChange={(e) => set('addressAr')(e.target.value)} />
          </Field>
          <Field label={t('latitude')} hint={t('locationHint')}>
            <input
              type="number"
              step="any"
              min={-90}
              max={90}
              inputMode="decimal"
              value={draft.latitude}
              onChange={(e) => set('latitude')(e.target.value)}
            />
          </Field>
          <Field label={t('longitude')}>
            <input
              type="number"
              step="any"
              min={-180}
              max={180}
              inputMode="decimal"
              value={draft.longitude}
              onChange={(e) => set('longitude')(e.target.value)}
            />
          </Field>
          <Field label={t('freeCancellationHours')} hint={t('freeCancellationHint')}>
            <input
              type="number"
              min={0}
              max={168}
              step={1}
              required
              inputMode="numeric"
              value={draft.freeCancellationHours}
              onChange={(e) => set('freeCancellationHours')(e.target.value)}
            />
          </Field>
        </fieldset>
        {canEdit && (
          <div className="form-actions">
            <Button type="submit">{t('save')}</Button>
          </div>
        )}
      </form>
    </section>
  );
}
