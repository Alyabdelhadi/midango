import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { LanguageToggle } from '../components/LanguageToggle';
import { Notice } from '../components/Notice';
import { errorText } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';

const MIN_LENGTH = 10;

export function ChangePasswordPage() {
  const { changePassword, signOut } = useAuth();
  const { t } = useI18n();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (next.length < MIN_LENGTH) return setError(t('passwordTooShort'));
    if (next !== confirm) return setError(t('passwordsDontMatch'));
    setBusy(true);
    setError(undefined);
    try {
      await changePassword(current, next);
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <div className="auth-page">
      <form className="auth-card" onSubmit={submit}>
        <img src="/midango-icon.png" alt="Midango" height={40} />
        <h1>{t('changePasswordTitle')}</h1>
        <p>{t('changePasswordIntro')}</p>
        {error && <Notice tone="error">{error}</Notice>}
        <Field label={t('currentPassword')}>
          <input type="password" required autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} />
        </Field>
        <Field label={t('newPassword')} hint={t('passwordTooShort')}>
          <input type="password" required minLength={MIN_LENGTH} autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} />
        </Field>
        <Field label={t('confirmPassword')}>
          <input type="password" required autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
        </Field>
        <Button type="submit" disabled={busy}>
          {t('savePassword')}
        </Button>
        <Button variant="text" onClick={() => void signOut()}>
          {t('signOut')}
        </Button>
        <LanguageToggle />
      </form>
    </div>
  );
}
