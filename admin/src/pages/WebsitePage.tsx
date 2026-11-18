import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { SiteContent, SiteFaq } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import type { MessageKey } from '../locales/en';
import { MoveButtons } from './AppContentPage';

/** Same limits the API enforces. */
const MAX_FAQS = 20;
const WEBSITE_URL: string = import.meta.env.VITE_WEBSITE_URL ?? 'http://localhost:5500/';

type Show = ReturnType<typeof useToast>['show'];
/** Runs one write; every write returns the whole GET shape, which replaces the page state. False = it failed. */
type Change = (action: () => Promise<SiteContent>, success?: string, show?: Show) => Promise<boolean>;

const swap = <T,>(list: T[], index: number, by: -1 | 1) => {
  const next = [...list];
  [next[index], next[index + by]] = [next[index + by], next[index]];
  return next;
};

export function WebsitePage() {
  const { t } = useI18n();
  const toast = useToast();
  const content = useLoad(() => api<SiteContent>('/admin/site-content'));

  const change: Change = async (action, success, show = toast.show) => {
    try {
      content.setData(await action());
      if (success) show('success', success);
      return true;
    } catch (err) {
      show('error', errorText(err));
      return false;
    }
  };

  const c = content.data;
  return (
    <div className="page">
      <PageHeader
        title={t('navWebsite')}
        action={
          <div className="chip-row">
            <a className="mg-button mg-button--secondary" href={WEBSITE_URL} target="_blank" rel="noopener noreferrer">
              {t('viewWebsite')}
            </a>
            <a className="mg-button mg-button--secondary" href={`${WEBSITE_URL}ar/`} target="_blank" rel="noopener noreferrer">
              {t('viewWebsiteAr')}
            </a>
          </div>
        }
      />
      <p className="page-intro">{t('websiteIntro')}</p>
      {content.error && <Notice tone="error">{content.error}</Notice>}
      {!c && !content.error && <p className="page-loading">{t('loading')}</p>}
      {c && (
        <>
          <TextsCard content={c} change={change} />
          <FaqCard content={c} change={change} />
          <ContactCard content={c} change={change} />
          <StoreLinksCard content={c} />
        </>
      )}
      {toast.view}
    </div>
  );
}

/* Texts */

const GROUPS: { prefixes: string[]; label: MessageKey }[] = [
  { prefixes: ['hero'], label: 'groupHero' },
  { prefixes: ['players', 'stores'], label: 'groupPlayers' },
  { prefixes: ['venues'], label: 'groupVenues' },
  { prefixes: ['request'], label: 'groupRequest' },
  { prefixes: ['faq'], label: 'groupFaq' },
  { prefixes: ['footer'], label: 'groupFooter' },
];

const TEXT_LABELS: Record<string, MessageKey> = {
  'hero.title': 'textHeroTitle',
  'hero.lede': 'textHeroLede',
  'players.kicker': 'textPlayersKicker',
  'players.title': 'textPlayersTitle',
  'players.lede': 'textPlayersLede',
  'players.step1.title': 'textStep1Title',
  'players.step1.body': 'textStep1Body',
  'players.step2.title': 'textStep2Title',
  'players.step2.body': 'textStep2Body',
  'players.step3.title': 'textStep3Title',
  'players.step3.body': 'textStep3Body',
  'stores.note': 'textStoresNote',
  'venues.kicker': 'textVenuesKicker',
  'venues.title': 'textVenuesTitle',
  'venues.lede': 'textVenuesLede',
  'request.kicker': 'textRequestKicker',
  'request.title': 'textRequestTitle',
  'request.lede': 'textRequestLede',
  'faq.title': 'textFaqTitle',
  'faq.lede': 'textFaqLede',
  'footer.tagline': 'textFooterTagline',
};

type Draft = Record<string, { en: string; ar: string }>;
const toDraft = (content: SiteContent): Draft => Object.fromEntries(content.texts.map((x) => [x.key, { en: x.en, ar: x.ar }]));

function TextsCard({ content, change }: { content: SiteContent; change: Change }) {
  const { t } = useI18n();
  const [draft, setDraft] = useState(() => toDraft(content));
  const [busy, setBusy] = useState(false);

  const changed = content.texts.filter((x) => draft[x.key].en !== x.en || draft[x.key].ar !== x.ar);
  const groupOf = (key: string) => GROUPS.find((g) => g.prefixes.includes(key.split('.')[0]))?.label ?? 'groupOther';
  // Seed order, split into sections; a section lists its keys in the order the API sends them.
  const sections = [...GROUPS.map((g) => g.label), 'groupOther' as const]
    .map((label) => ({ label, texts: content.texts.filter((x) => groupOf(x.key) === label) }))
    .filter((s) => s.texts.length > 0);

  const edit = (key: string, lang: 'en' | 'ar', value: string) =>
    setDraft((d) => ({ ...d, [key]: { ...d[key], [lang]: value } }));

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      let next: SiteContent | undefined;
      const ok = await change(async () => {
        next = await api<SiteContent>('/admin/site-content/texts', {
          method: 'PUT',
          body: changed.map((x) => ({ key: x.key, ...draft[x.key] })),
        });
        return next;
      }, t('saved'));
      if (ok && next) setDraft(toDraft(next));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card">
      <h2>{t('websiteTextsTitle')}</h2>
      <p className="mg-field-hint">{t('websiteTextsHint')}</p>
      <form className="dialog-form" onSubmit={(e) => void save(e)}>
        {sections.map((s) => (
          <fieldset key={s.label} className="mg-fieldset">
            <legend>{t(s.label)}</legend>
            {s.texts.map((x) => {
              const label = TEXT_LABELS[x.key] ? t(TEXT_LABELS[x.key]) : x.key;
              return (
                <div key={x.key} className="dialog-form">
                  <h3 className="media-subhead">{label}</h3>
                  <div className="form-grid">
                    <Field label={t('siteEnglish')}>
                      <textarea
                        required
                        rows={2}
                        maxLength={600}
                        dir="ltr"
                        lang="en"
                        value={draft[x.key].en}
                        onChange={(e) => edit(x.key, 'en', e.target.value)}
                      />
                    </Field>
                    <Field label={t('siteArabic')}>
                      <textarea
                        required
                        rows={2}
                        maxLength={600}
                        dir="rtl"
                        lang="ar"
                        value={draft[x.key].ar}
                        onChange={(e) => edit(x.key, 'ar', e.target.value)}
                      />
                    </Field>
                  </div>
                </div>
              );
            })}
          </fieldset>
        ))}
        <div className="form-actions">
          {changed.length > 0 && <span className="legal-unsaved">{t('unsavedChanges')}</span>}
          <Button type="submit" disabled={busy || changed.length === 0}>
            {busy ? t('saving') : t('save')}
          </Button>
        </div>
      </form>
    </section>
  );
}

/* FAQ */

type FaqDialog = { kind: 'edit'; faq: SiteFaq | null } | { kind: 'delete'; faq: SiteFaq } | null;

function FaqCard({ content, change }: { content: SiteContent; change: Change }) {
  const { t, lang } = useI18n();
  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<FaqDialog>(null);
  const faqs = content.faqs;
  const question = (f: SiteFaq) => (lang === 'ar' ? f.questionAr : f.questionEn);

  async function guarded(work: () => Promise<unknown>) {
    setBusy(true);
    try {
      await work();
    } finally {
      setBusy(false);
    }
  }

  const move = (index: number, by: -1 | 1) =>
    guarded(() =>
      change(() =>
        api<SiteContent>('/admin/site-content/faqs/order', {
          method: 'PUT',
          body: swap(faqs.map((f) => f.id), index, by),
        }),
      ),
    );

  const remove = (f: SiteFaq) =>
    guarded(async () => {
      setDialog(null);
      await change(() => api<SiteContent>(`/admin/site-content/faqs/${f.id}`, { method: 'DELETE' }), t('faqDeleted'));
    });

  const editing = dialog?.kind === 'edit' ? dialog : null;

  return (
    <section className="card">
      <div className="card-head">
        <h2>{t('faqTitle')}</h2>
        <span className="mg-field-hint">{t('bannerCount', { n: faqs.length, max: MAX_FAQS })}</span>
      </div>
      <p className="mg-field-hint">{t('faqHint')}</p>
      {faqs.length === 0 ? (
        <p className="empty">{t('noFaqs')}</p>
      ) : (
        <ol className="content-list">
          {faqs.map((f, index) => (
            <li key={f.id} className="content-row">
              <div className="content-row-main">
                <strong>{question(f)}</strong>
              </div>
              <div className="content-row-actions">
                <MoveButtons index={index} count={faqs.length} busy={busy} onMove={(by) => void move(index, by)} />
                <Button variant="secondary" disabled={busy} onClick={() => setDialog({ kind: 'edit', faq: f })}>
                  {t('edit')}
                </Button>
                <Button variant="text" disabled={busy} onClick={() => setDialog({ kind: 'delete', faq: f })}>
                  {t('deleteAction')}
                </Button>
              </div>
            </li>
          ))}
        </ol>
      )}
      <div>
        <Button
          disabled={busy}
          disabledReason={faqs.length >= MAX_FAQS ? t('faqsFull') : undefined}
          onClick={() => setDialog({ kind: 'edit', faq: null })}
        >
          {t('addFaq')}
        </Button>
      </div>

      <Dialog open={editing !== null} title={editing?.faq ? t('editFaq') : t('newFaq')} onClose={() => setDialog(null)}>
        {editing && (
          <FaqForm key={editing.faq?.id ?? 'new'} faq={editing.faq} change={change} onDone={() => setDialog(null)} />
        )}
      </Dialog>

      <Dialog open={dialog?.kind === 'delete'} title={t('deleteFaqTitle')} onClose={() => setDialog(null)}>
        {dialog?.kind === 'delete' && (
          <div className="dialog-form">
            <p>{t('deleteFaqConfirm', { question: question(dialog.faq) })}</p>
            <div className="form-actions">
              <Button variant="secondary" onClick={() => setDialog(null)}>
                {t('cancel')}
              </Button>
              <Button variant="destructive" disabled={busy} onClick={() => void remove(dialog.faq)}>
                {t('deleteAction')}
              </Button>
            </div>
          </div>
        )}
      </Dialog>
    </section>
  );
}

function FaqForm({ faq, change, onDone }: { faq: SiteFaq | null; change: Change; onDone: () => void }) {
  const { t } = useI18n();
  const [questionEn, setQuestionEn] = useState(faq?.questionEn ?? '');
  const [questionAr, setQuestionAr] = useState(faq?.questionAr ?? '');
  const [answerEn, setAnswerEn] = useState(faq?.answerEn ?? '');
  const [answerAr, setAnswerAr] = useState(faq?.answerAr ?? '');
  const [busy, setBusy] = useState(false);
  // In-dialog toast: the page toast sits behind the modal.
  const toast = useToast();

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      const body = { questionEn, questionAr, answerEn, answerAr };
      const ok = await change(
        () =>
          faq
            ? api<SiteContent>(`/admin/site-content/faqs/${faq.id}`, { method: 'PUT', body })
            : api<SiteContent>('/admin/site-content/faqs', { method: 'POST', body }),
        t('saved'),
        toast.show,
      );
      if (ok) onDone();
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={(e) => void submit(e)}>
      <div className="form-grid">
        <Field label={t('questionEn')}>
          <input required maxLength={200} dir="ltr" lang="en" value={questionEn} onChange={(e) => setQuestionEn(e.target.value)} />
        </Field>
        <Field label={t('questionAr')}>
          <input required maxLength={200} dir="rtl" lang="ar" value={questionAr} onChange={(e) => setQuestionAr(e.target.value)} />
        </Field>
        <Field label={t('answerEn')}>
          <textarea required rows={5} maxLength={1000} dir="ltr" lang="en" value={answerEn} onChange={(e) => setAnswerEn(e.target.value)} />
        </Field>
        <Field label={t('answerAr')}>
          <textarea required rows={5} maxLength={1000} dir="rtl" lang="ar" value={answerAr} onChange={(e) => setAnswerAr(e.target.value)} />
        </Field>
      </div>
      {toast.view}
      <div className="form-actions">
        <Button variant="secondary" onClick={onDone}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? t('saving') : t('save')}
        </Button>
      </div>
    </form>
  );
}

/* Contact */

function ContactCard({ content, change }: { content: SiteContent; change: Change }) {
  const { t } = useI18n();
  const [email, setEmail] = useState(content.contact.email ?? '');
  const [phone, setPhone] = useState(content.contact.phone ?? '');
  const [whatsApp, setWhatsApp] = useState(content.contact.whatsApp ?? '');
  const [instagram, setInstagram] = useState(content.contact.instagram ?? '');
  const [busy, setBusy] = useState(false);

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    const value = (s: string) => s.trim() || null;
    try {
      await change(
        () =>
          api<SiteContent>('/admin/site-content/contact', {
            method: 'PUT',
            body: { email: value(email), phone: value(phone), whatsApp: value(whatsApp), instagram: value(instagram) },
          }),
        t('saved'),
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card">
      <h2>{t('contactTitle')}</h2>
      <p className="mg-field-hint">{t('contactHint')}</p>
      <form className="dialog-form" onSubmit={(e) => void save(e)}>
        <div className="form-grid">
          <Field label={t('contactEmail')}>
            <input type="email" maxLength={120} dir="ltr" value={email} onChange={(e) => setEmail(e.target.value)} />
          </Field>
          <Field label={t('contactPhone')}>
            <input type="tel" maxLength={30} dir="ltr" value={phone} onChange={(e) => setPhone(e.target.value)} />
          </Field>
          <Field label={t('contactWhatsApp')}>
            <input type="tel" maxLength={30} dir="ltr" value={whatsApp} onChange={(e) => setWhatsApp(e.target.value)} />
          </Field>
          <Field label={t('contactInstagram')} hint={t('instagramHint')}>
            <input maxLength={60} dir="ltr" value={instagram} onChange={(e) => setInstagram(e.target.value)} />
          </Field>
        </div>
        <div className="form-actions">
          <Button type="submit" disabled={busy}>
            {busy ? t('saving') : t('save')}
          </Button>
        </div>
      </form>
    </section>
  );
}

/* Store links (owned by App content) */

function StoreLinksCard({ content }: { content: SiteContent }) {
  const { t } = useI18n();
  const links: [MessageKey, string | null][] = [
    ['iosStoreUrl', content.iosStoreUrl],
    ['androidStoreUrl', content.androidStoreUrl],
  ];
  return (
    <section className="card">
      <div className="card-head">
        <h2>{t('storeLinksTitle')}</h2>
        <Link to="/app-content">{t('editInAppContent')}</Link>
      </div>
      <p className="mg-field-hint">{t('storeLinksHint')}</p>
      <dl className="form-grid store-links">
        {links.map(([label, url]) => (
          <div key={label}>
            <dt className="mg-field-label">{t(label)}</dt>
            <dd className="muted">
              {url ? <bdi dir="ltr">{url}</bdi> : t('notSet')}
            </dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
