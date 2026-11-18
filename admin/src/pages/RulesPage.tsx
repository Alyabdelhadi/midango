import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { PlatformSettings } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import type { MessageKey } from '../locales/en';

/** Same ranges the API enforces. */
const FIELDS: { key: keyof PlatformSettings; label: MessageKey; min: number; max: number }[] = [
  { key: 'reminderHoursBefore', label: 'reminderHours', min: 1, max: 24 },
  { key: 'noShowLimit', label: 'noShowLimit', min: 1, max: 10 },
  { key: 'noShowWindowDays', label: 'noShowWindow', min: 7, max: 180 },
  { key: 'limitedUpcomingBookings', label: 'limitedUpcoming', min: 0, max: 5 },
];

export function RulesPage() {
  const { t } = useI18n();
  const settings = useLoad(() => api<PlatformSettings>('/admin/settings'));

  if (settings.error) {
    return (
      <div className="page">
        <Notice tone="error">{settings.error}</Notice>
      </div>
    );
  }
  if (!settings.data) return <p className="page-loading">{t('loading')}</p>;

  return (
    <div className="page">
      <PageHeader title={t('navRules')} />
      <p className="page-intro">{t('rulesIntro')}</p>
      <RulesForm initial={settings.data} onSaved={settings.setData} />
    </div>
  );
}

function RulesForm({ initial, onSaved }: { initial: PlatformSettings; onSaved: (s: PlatformSettings) => void }) {
  const { t } = useI18n();
  const toast = useToast();
  const [values, setValues] = useState(() =>
    Object.fromEntries(FIELDS.map((f) => [f.key, String(initial[f.key])])) as Record<keyof PlatformSettings, string>,
  );
  const [busy, setBusy] = useState(false);

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      const body = Object.fromEntries(FIELDS.map((f) => [f.key, Number(values[f.key])])) as PlatformSettings;
      onSaved(await api<PlatformSettings>('/admin/settings', { method: 'PUT', body }));
      toast.show('success', t('saved'));
    } catch (err) {
      toast.show('error', errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="card" onSubmit={save}>
      <div className="form-grid rules-grid">
        {FIELDS.map((f) => (
          <Field key={f.key} label={t(f.label)}>
            <input
              type="number"
              required
              min={f.min}
              max={f.max}
              step={1}
              inputMode="numeric"
              value={values[f.key]}
              onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))}
            />
          </Field>
        ))}
      </div>
      <p className="hours-note">
        {t('rulesSummary', { n: values.noShowLimit, d: values.noShowWindowDays, k: values.limitedUpcomingBookings })}
      </p>
      {toast.view}
      <div className="form-actions">
        <Button type="submit" disabled={busy}>
          {t('save')}
        </Button>
      </div>
    </form>
  );
}
