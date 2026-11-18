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

type RuleField = { key: keyof PlatformSettings; label: MessageKey; min: number; max: number; step?: number };

/** Same ranges the API enforces. */
const FIELDS: RuleField[] = [
  { key: 'reminderHoursBefore', label: 'reminderHours', min: 1, max: 24 },
  { key: 'noShowLimit', label: 'noShowLimit', min: 1, max: 10 },
  { key: 'noShowWindowDays', label: 'noShowWindow', min: 7, max: 180 },
  { key: 'limitedUpcomingBookings', label: 'limitedUpcoming', min: 0, max: 5 },
];

/** Same ranges the API enforces; the API also wants Gold to need more games than Silver. */
const LOYALTY_FIELDS: RuleField[] = [
  { key: 'pointsPerSar', label: 'pointsPerSar', min: 0, max: 100, step: 0.01 },
  { key: 'silverGames', label: 'silverGames', min: 1, max: 500 },
  { key: 'silverMultiplier', label: 'silverMultiplier', min: 1, max: 5, step: 0.01 },
  { key: 'goldGames', label: 'goldGames', min: 1, max: 500 },
  { key: 'goldMultiplier', label: 'goldMultiplier', min: 1, max: 5, step: 0.01 },
  { key: 'tierWindowDays', label: 'tierWindowDays', min: 7, max: 365 },
  { key: 'pointsPerRedeemStep', label: 'pointsPerRedeemStep', min: 1, max: 100_000 },
  { key: 'sarPerRedeemStep', label: 'sarPerRedeemStep', min: 0.01, max: 1000, step: 0.01 },
  { key: 'maxRedeemPercent', label: 'maxRedeemPercent', min: 0, max: 100 },
];

const ALL = [...FIELDS, ...LOYALTY_FIELDS];

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
  const [values, setValues] = useState(
    () => Object.fromEntries(ALL.map((f) => [f.key, String(initial[f.key])])) as Record<keyof PlatformSettings, string>,
  );
  const [busy, setBusy] = useState(false);

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      const body = Object.fromEntries(ALL.map((f) => [f.key, Number(values[f.key])])) as PlatformSettings;
      onSaved(await api<PlatformSettings>('/admin/settings', { method: 'PUT', body }));
      toast.show('success', t('saved'));
    } catch (err) {
      toast.show('error', errorText(err));
    } finally {
      setBusy(false);
    }
  }

  const inputs = (fields: RuleField[]) => (
    <div className="form-grid rules-grid">
      {fields.map((f) => (
        <Field key={f.key} label={t(f.label)}>
          <input
            type="number"
            required
            min={f.min}
            max={f.max}
            step={f.step ?? 1}
            inputMode={f.step ? 'decimal' : 'numeric'}
            value={values[f.key]}
            onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))}
          />
        </Field>
      ))}
    </div>
  );

  // The live example: what a Silver player paying SAR 100 earns, and what one redeem step is worth.
  const earned = Math.floor(100 * Number(values.pointsPerSar) * Number(values.silverMultiplier));

  return (
    <form className="rules-form" onSubmit={save}>
      <section className="card">
        {inputs(FIELDS)}
        <p className="hours-note">
          {t('rulesSummary', { n: values.noShowLimit, d: values.noShowWindowDays, k: values.limitedUpcomingBookings })}
        </p>
      </section>
      <section className="card">
        <h2>{t('loyaltyTitle')}</h2>
        {inputs(LOYALTY_FIELDS)}
        <p className="hours-note">
          {t('loyaltyExample', {
            points: Number.isFinite(earned) ? earned : 0,
            step: values.pointsPerRedeemStep,
            sar: values.sarPerRedeemStep,
          })}
        </p>
      </section>
      {toast.view}
      <div className="form-actions">
        <Button type="submit" disabled={busy}>
          {t('save')}
        </Button>
      </div>
    </form>
  );
}
