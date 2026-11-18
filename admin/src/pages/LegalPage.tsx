import { useCallback, useEffect, useState, type FormEvent, type KeyboardEvent } from 'react';
import { Button } from '../components/Button';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { Markdown } from '../lib/markdown';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import type { MessageKey } from '../locales/en';

type Kind = 'privacy' | 'terms';
type LegalDocument = { kind: Kind; bodyEn: string; bodyAr: string; updatedAt: string };

const KINDS: { kind: Kind; label: MessageKey }[] = [
  { kind: 'privacy', label: 'legalPrivacy' },
  { kind: 'terms', label: 'legalTerms' },
];

/** Same limit the API enforces. */
const MAX_LENGTH = 60_000;

export function LegalPage() {
  const { t } = useI18n();
  const [active, setActive] = useState<Kind>('privacy');
  const [dirty, setDirty] = useState<Record<Kind, boolean>>({ privacy: false, terms: false });
  const onDirty = useCallback((kind: Kind, value: boolean) => setDirty((d) => ({ ...d, [kind]: value })), []);

  // Two tabs: every arrow key, Home and End move to the other one.
  function onTabKey(e: KeyboardEvent) {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(e.key)) return;
    e.preventDefault();
    const next: Kind = e.key === 'Home' ? 'privacy' : e.key === 'End' ? 'terms' : active === 'privacy' ? 'terms' : 'privacy';
    setActive(next);
    document.getElementById(`legal-tab-${next}`)?.focus();
  }

  return (
    <div className="page">
      <PageHeader title={t('navLegal')} />
      <p className="page-intro">{t('legalIntro')}</p>
      <div className="legal-tabs" role="tablist" aria-label={t('navLegal')} onKeyDown={onTabKey}>
        {KINDS.map(({ kind, label }) => (
          <button
            key={kind}
            id={`legal-tab-${kind}`}
            type="button"
            role="tab"
            className="legal-tab"
            aria-selected={active === kind}
            aria-controls={`legal-panel-${kind}`}
            tabIndex={active === kind ? 0 : -1}
            onClick={() => setActive(kind)}
          >
            {t(label)}
            {dirty[kind] && <span className="legal-tab-dirty">{t('unsavedChanges')}</span>}
          </button>
        ))}
      </div>
      {/* Both stay mounted, so switching tabs keeps unsaved edits. */}
      {KINDS.map(({ kind }) => (
        <div
          key={kind}
          id={`legal-panel-${kind}`}
          role="tabpanel"
          aria-labelledby={`legal-tab-${kind}`}
          hidden={active !== kind}
        >
          <LegalEditor kind={kind} onDirty={onDirty} />
        </div>
      ))}
    </div>
  );
}

function LegalEditor({ kind, onDirty }: { kind: Kind; onDirty: (kind: Kind, dirty: boolean) => void }) {
  const { t } = useI18n();
  const doc = useLoad(() => api<LegalDocument>(`/admin/legal/${kind}`));

  if (doc.error) return <Notice tone="error">{doc.error}</Notice>;
  if (!doc.data) return <p className="page-loading">{t('loading')}</p>;
  return <LegalForm initial={doc.data} onSaved={doc.setData} onDirty={onDirty} />;
}

function LegalForm({
  initial,
  onSaved,
  onDirty,
}: {
  initial: LegalDocument;
  onSaved: (doc: LegalDocument) => void;
  onDirty: (kind: Kind, dirty: boolean) => void;
}) {
  const { t, lang } = useI18n();
  const toast = useToast();
  const [bodyEn, setBodyEn] = useState(initial.bodyEn);
  const [bodyAr, setBodyAr] = useState(initial.bodyAr);
  const [busy, setBusy] = useState(false);
  const dirty = bodyEn !== initial.bodyEn || bodyAr !== initial.bodyAr;

  useEffect(() => onDirty(initial.kind, dirty), [initial.kind, dirty, onDirty]);

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      const saved = await api<LegalDocument>(`/admin/legal/${initial.kind}`, {
        method: 'PUT',
        body: { bodyEn, bodyAr },
      });
      onSaved(saved);
      setBodyEn(saved.bodyEn);
      setBodyAr(saved.bodyAr);
      toast.show('success', t('saved'));
    } catch (err) {
      toast.show('error', errorText(err));
    } finally {
      setBusy(false);
    }
  }

  const updated = new Intl.DateTimeFormat(lang === 'ar' ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(initial.updatedAt));

  const columns = [
    { id: 'en', label: t('legalEnglish'), value: bodyEn, set: setBodyEn, dir: 'ltr', lang: 'en' },
    { id: 'ar', label: t('legalArabic'), value: bodyAr, set: setBodyAr, dir: 'rtl', lang: 'ar' },
  ] as const;

  return (
    <form className="card" onSubmit={save}>
      <p className="hours-note">
        {t('legalUpdated', { date: updated })} · {t('legalFormatHint')}
      </p>
      <div className="legal-columns">
        {columns.map((c) => {
          const fieldId = `legal-${initial.kind}-${c.id}`;
          return (
            <div key={c.id} className="legal-column">
              <label className="mg-field-label" htmlFor={fieldId}>
                {c.label}
              </label>
              <textarea
                id={fieldId}
                dir={c.dir}
                lang={c.lang}
                required
                maxLength={MAX_LENGTH}
                value={c.value}
                onChange={(e) => c.set(e.target.value)}
              />
              <span className="mg-field-label">{t('legalPreview')}</span>
              <div className="legal-preview" dir={c.dir} lang={c.lang}>
                <Markdown source={c.value} />
              </div>
            </div>
          );
        })}
      </div>
      {toast.view}
      <div className="form-actions">
        {dirty && <span className="legal-unsaved">{t('unsavedChanges')}</span>}
        <Button type="submit" disabled={busy}>
          {busy ? t('saving') : t('save')}
        </Button>
      </div>
    </form>
  );
}
