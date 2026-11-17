import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { Role } from '../lib/types';

/** Creates a staff account with a temporary password. `role` is sent only where the endpoint takes one. */
export function StaffForm({
  path,
  role,
  onCancel,
  onSaved,
}: {
  path: string;
  role?: Role;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const { t } = useI18n();
  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api(path, { method: 'POST', body: { email, displayName, temporaryPassword, ...(role ? { role } : {}) } });
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
      <Field label={t('displayName')}>
        <input required maxLength={80} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
      </Field>
      <Field label={t('email')}>
        <input type="email" required autoComplete="off" value={email} onChange={(e) => setEmail(e.target.value)} />
      </Field>
      <Field label={t('temporaryPassword')} hint={t('temporaryPasswordHint')}>
        <input
          required
          minLength={10}
          autoComplete="new-password"
          value={temporaryPassword}
          onChange={(e) => setTemporaryPassword(e.target.value)}
        />
      </Field>
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
