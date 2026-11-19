import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router';
import { Building2, CalendarCheck, Eye, EyeOff, LockKeyhole, Mail } from 'lucide-react';
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
  const [remember, setRemember] = useState(true);
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  if (status === 'signedIn') return <Navigate to="/" replace />;

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await signIn(email, password, remember);
      navigate('/', { replace: true });
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <div className="auth-split">
      <aside className="auth-hero">
        <div className="auth-brand">
          <img src="/midango-icon.png" alt="" height={44} />
          <span>
            Midango<small>{t('appBusiness')}</small>
          </span>
        </div>
        <div className="auth-hero-copy">
          <h2>
            {t('signInHeroLead')} <em>{t('signInHeroAccent')}</em>
          </h2>
          <p>{t('signInHeroBody')}</p>
        </div>
        <dl className="auth-hero-facts">
          <div>
            <dt>
              <Building2 aria-hidden size={26} />
              {t('signInFact1')}
            </dt>
            <dd>{t('signInFact1Label')}</dd>
          </div>
          <div>
            <dt>
              <CalendarCheck aria-hidden size={26} />
              {t('signInFact2')}
            </dt>
            <dd>{t('signInFact2Label')}</dd>
          </div>
        </dl>
      </aside>
      <form className="auth-form" onSubmit={submit}>
        <header>
          <h1>{t('signInWelcome')}</h1>
          <p>{t('signInTitle')}</p>
        </header>
        {error && <Notice tone="error">{error}</Notice>}
        <Field label={t('email')}>
          <span className="auth-input">
            <Mail aria-hidden size={18} />
            <input type="email" required autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} />
          </span>
        </Field>
        <Field label={t('password')}>
          <span className="auth-input">
            <LockKeyhole aria-hidden size={18} />
            <input
              type={showPassword ? 'text' : 'password'}
              aria-label={t('password')}
              required
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <button
              type="button"
              className="auth-eye"
              aria-label={t(showPassword ? 'hidePassword' : 'showPassword')}
              aria-pressed={showPassword}
              onClick={() => setShowPassword((v) => !v)}
            >
              {showPassword ? <EyeOff aria-hidden size={18} /> : <Eye aria-hidden size={18} />}
            </button>
          </span>
        </Field>
        <label className="check-row">
          <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
          {t('rememberMe')}
        </label>
        <Button type="submit" disabled={busy}>
          {t('signIn')}
        </Button>
      </form>
      <div className="auth-lang">
        <LanguageToggle />
      </div>
    </div>
  );
}
