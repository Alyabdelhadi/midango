import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { LanguageToggle } from '../components/LanguageToggle';
import { Notice } from '../components/Notice';
import { errorText } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';

export function SignInPage() {
  const { status, signIn } = useAuth();
  const { t } = useI18n();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  if (status === 'signedIn') return <Navigate to="/" replace />;

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await signIn(email, password);
      navigate('/', { replace: true });
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <div className="auth-page">
      <form className="auth-card" onSubmit={submit}>
        <img src="/midango-icon.png" alt="Midango" height={40} />
        <h1>{t('signInTitle')}</h1>
        {error && <Notice tone="error">{error}</Notice>}
        <Field label={t('email')}>
          <input type="email" required autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} />
        </Field>
        <Field label={t('password')}>
          <input
            type="password"
            required
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </Field>
        <Button type="submit" disabled={busy}>
          {t('signIn')}
        </Button>
        <LanguageToggle />
      </form>
    </div>
  );
}
