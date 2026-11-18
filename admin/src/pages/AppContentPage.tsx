import { ChevronDown, ChevronUp, ImageOff } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { UploadButton } from '../components/UploadButton';
import { api, errorText, fileBody, mediaUrl } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { formatUtcDate } from '../lib/time';
import type { AppContent, Banner, BannerLinkType, Broadcast } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import type { MessageKey } from '../locales/en';

/** Same limits the API enforces. */
const MAX_BANNERS = 10;
const MAX_FEATURED = 10;

type Show = ReturnType<typeof useToast>['show'];
/** Runs one write; every write returns the whole GET shape, which replaces the page state. False = it failed. */
type Change = (action: () => Promise<AppContent>, success?: string, show?: Show) => Promise<boolean>;

const swap = <T,>(list: T[], index: number, by: -1 | 1) => {
  const next = [...list];
  [next[index], next[index + by]] = [next[index + by], next[index]];
  return next;
};

export function AppContentPage() {
  const { t } = useI18n();
  const toast = useToast();
  const content = useLoad(() => api<AppContent>('/admin/app-content'));

  // `show` lets a form inside an open dialog report there: the page toast sits behind the modal.
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
      <PageHeader title={t('navAppContent')} />
      <p className="page-intro">{t('appContentIntro')}</p>
      {content.error && <Notice tone="error">{content.error}</Notice>}
      {!c && !content.error && <p className="page-loading">{t('loading')}</p>}
      {c && (
        <>
          <BannersCard content={c} change={change} />
          <FeaturedCard content={c} change={change} />
          <BroadcastCard show={toast.show} />
          <VersionCard content={c} change={change} />
        </>
      )}
      {toast.view}
    </div>
  );
}

/* Home banners */

type BannerDialog = { kind: 'edit'; id: string | null } | { kind: 'delete'; banner: Banner } | null;

function BannersCard({ content, change }: { content: AppContent; change: Change }) {
  const { t, name } = useI18n();
  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<BannerDialog>(null);
  const banners = content.banners;

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
        api<AppContent>('/admin/app-content/banners/order', {
          method: 'PUT',
          body: swap(banners.map((b) => b.id), index, by),
        }),
      ),
    );

  const toggle = (b: Banner) =>
    guarded(() =>
      change(() =>
        api<AppContent>(`/admin/app-content/banners/${b.id}`, {
          method: 'PUT',
          body: { ...bannerInput(b), active: !b.active },
        }),
      ),
    );

  const remove = (b: Banner) =>
    guarded(async () => {
      setDialog(null);
      await change(() => api<AppContent>(`/admin/app-content/banners/${b.id}`, { method: 'DELETE' }), t('bannerDeleted'));
    });

  function linkSummary(b: Banner) {
    const target = b.linkTarget ?? '';
    if (b.linkType === 'Venue') {
      const v = content.venues.find((x) => x.id === target);
      return t('linkVenueTo', { name: v ? name(v) : t('linkUnavailable') });
    }
    if (b.linkType === 'Tournament') {
      const x = content.tournaments.find((y) => y.id === target);
      return t('linkTournamentTo', { name: x ? name(x) : t('linkUnavailable') });
    }
    if (b.linkType === 'Url') return <bdi dir="ltr">{target}</bdi>;
    return null;
  }

  const editing = dialog?.kind === 'edit' ? dialog : null;
  const editingBanner = editing?.id ? banners.find((b) => b.id === editing.id) : undefined;

  return (
    <section className="card">
      <div className="card-head">
        <h2>{t('bannersTitle')}</h2>
        <span className="mg-field-hint">{t('bannerCount', { n: banners.length, max: MAX_BANNERS })}</span>
      </div>
      <p className="mg-field-hint">{t('bannersHint')}</p>
      {banners.length === 0 ? (
        <p className="empty">{t('noBanners')}</p>
      ) : (
        <ol className="content-list">
          {banners.map((b, index) => (
            <li key={b.id} className="content-row">
              {b.imageUrl ? (
                <img className="banner-thumb" src={mediaUrl(b.imageUrl)} alt="" />
              ) : (
                <span className="banner-thumb media-placeholder">
                  <ImageOff size={20} aria-hidden="true" />
                  <span className="visually-hidden">{t('noImage')}</span>
                </span>
              )}
              <div className="content-row-main">
                <strong>{name({ nameEn: b.titleEn, nameAr: b.titleAr })}</strong>
                <span className="pill-row">
                  <Pill tone={b.active ? 'success' : 'neutral'}>{b.active ? t('active') : t('bannerOff')}</Pill>
                </span>
                {b.linkType !== 'None' && <span className="muted">{linkSummary(b)}</span>}
              </div>
              <div className="content-row-actions">
                <MoveButtons index={index} count={banners.length} busy={busy} onMove={(by) => void move(index, by)} />
                <Button variant="secondary" disabled={busy} onClick={() => void toggle(b)}>
                  {b.active ? t('turnOff') : t('turnOn')}
                </Button>
                <Button variant="secondary" disabled={busy} onClick={() => setDialog({ kind: 'edit', id: b.id })}>
                  {t('edit')}
                </Button>
                <Button variant="text" disabled={busy} onClick={() => setDialog({ kind: 'delete', banner: b })}>
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
          disabledReason={banners.length >= MAX_BANNERS ? t('bannersFull') : undefined}
          onClick={() => setDialog({ kind: 'edit', id: null })}
        >
          {t('addBanner')}
        </Button>
      </div>

      <Dialog
        open={editing !== null}
        title={editingBanner ? t('editBanner') : t('newBanner')}
        onClose={() => setDialog(null)}
      >
        <BannerForm
          // A fresh form once the new banner exists, so it carries the saved values and the image controls.
          key={editingBanner?.id ?? 'new'}
          banner={editingBanner}
          content={content}
          change={change}
          onCreated={(id) => setDialog({ kind: 'edit', id })}
          onDone={() => setDialog(null)}
        />
      </Dialog>

      <Dialog open={dialog?.kind === 'delete'} title={t('deleteBannerTitle')} onClose={() => setDialog(null)}>
        {dialog?.kind === 'delete' && (
          <div className="dialog-form">
            <p>{t('deleteBannerConfirm', { title: name({ nameEn: dialog.banner.titleEn, nameAr: dialog.banner.titleAr }) })}</p>
            <div className="form-actions">
              <Button variant="secondary" onClick={() => setDialog(null)}>
                {t('cancel')}
              </Button>
              <Button variant="destructive" disabled={busy} onClick={() => void remove(dialog.banner)}>
                {t('deleteAction')}
              </Button>
            </div>
          </div>
        )}
      </Dialog>
    </section>
  );
}

const bannerInput = (b: Banner) => ({
  titleEn: b.titleEn,
  titleAr: b.titleAr,
  bodyEn: b.bodyEn,
  bodyAr: b.bodyAr,
  linkType: b.linkType,
  linkTarget: b.linkTarget,
  active: b.active,
});

const LINK_TYPES: { value: BannerLinkType; label: MessageKey }[] = [
  { value: 'None', label: 'linkNone' },
  { value: 'Venue', label: 'linkVenue' },
  { value: 'Tournament', label: 'linkTournament' },
  { value: 'Url', label: 'linkUrl' },
];

function BannerForm({
  banner,
  content,
  change,
  onCreated,
  onDone,
}: {
  banner?: Banner;
  content: AppContent;
  change: Change;
  onCreated: (id: string) => void;
  onDone: () => void;
}) {
  const { t, name, lang } = useI18n();
  const [titleEn, setTitleEn] = useState(banner?.titleEn ?? '');
  const [titleAr, setTitleAr] = useState(banner?.titleAr ?? '');
  const [bodyEn, setBodyEn] = useState(banner?.bodyEn ?? '');
  const [bodyAr, setBodyAr] = useState(banner?.bodyAr ?? '');
  const [active, setActive] = useState(banner?.active ?? true);
  const [linkType, setLinkType] = useState<BannerLinkType>(banner?.linkType ?? 'None');
  const [linkTarget, setLinkTarget] = useState(banner?.linkTarget ?? '');
  const [busy, setBusy] = useState(false);
  const toast = useToast();

  async function guarded(work: () => Promise<unknown>) {
    setBusy(true);
    try {
      await work();
    } finally {
      setBusy(false);
    }
  }

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const body = {
      titleEn,
      titleAr,
      bodyEn,
      bodyAr,
      linkType,
      linkTarget: linkType === 'None' ? null : linkTarget,
      active,
    };
    return guarded(async () => {
      if (banner) {
        if (await change(() => api<AppContent>(`/admin/app-content/banners/${banner.id}`, { method: 'PUT', body }), t('saved'), toast.show))
          onDone();
        return;
      }
      const before = new Set(content.banners.map((b) => b.id));
      let created: string | undefined;
      const ok = await change(async () => {
        const next = await api<AppContent>('/admin/app-content/banners', { method: 'POST', body });
        created = next.banners.find((b) => !before.has(b.id))?.id;
        return next;
      }, t('bannerCreated'), toast.show);
      if (ok && created) onCreated(created);
    });
  };

  const image = (file?: File) =>
    guarded(() =>
      change(() =>
        api<AppContent>(`/admin/app-content/banners/${banner!.id}/image`, {
          method: file ? 'PUT' : 'DELETE',
          body: file ? fileBody(file) : undefined,
        }),
        undefined,
        toast.show,
      ),
    );

  const when = (iso: string) => formatUtcDate(iso, lang);

  return (
    <form className="dialog-form" onSubmit={(e) => void submit(e)}>
      <div className="form-grid">
        <Field label={t('titleEn')}>
          <input required maxLength={80} dir="ltr" lang="en" value={titleEn} onChange={(e) => setTitleEn(e.target.value)} />
        </Field>
        <Field label={t('titleAr')}>
          <input required maxLength={80} dir="rtl" lang="ar" value={titleAr} onChange={(e) => setTitleAr(e.target.value)} />
        </Field>
        <Field label={t('textEn')}>
          <textarea rows={3} maxLength={200} dir="ltr" lang="en" value={bodyEn} onChange={(e) => setBodyEn(e.target.value)} />
        </Field>
        <Field label={t('textAr')}>
          <textarea rows={3} maxLength={200} dir="rtl" lang="ar" value={bodyAr} onChange={(e) => setBodyAr(e.target.value)} />
        </Field>
      </div>

      <div className="form-grid">
        <Field label={t('linkType')}>
          <select
            value={linkType}
            onChange={(e) => {
              setLinkType(e.target.value as BannerLinkType);
              setLinkTarget('');
            }}
          >
            {LINK_TYPES.map((l) => (
              <option key={l.value} value={l.value}>
                {t(l.label)}
              </option>
            ))}
          </select>
        </Field>
        {linkType === 'Venue' && (
          <Field label={t('linkVenue')}>
            <select required value={linkTarget} onChange={(e) => setLinkTarget(e.target.value)}>
              <option value="">{t('chooseVenue')}</option>
              {content.venues.map((v) => (
                <option key={v.id} value={v.id}>
                  {name(v)}
                </option>
              ))}
            </select>
          </Field>
        )}
        {linkType === 'Tournament' && (
          <Field label={t('linkTournament')}>
            <select required value={linkTarget} onChange={(e) => setLinkTarget(e.target.value)}>
              <option value="">{t('chooseTournament')}</option>
              {content.tournaments.map((x) => (
                <option key={x.id} value={x.id}>
                  {name(x)} · {when(x.startsAt)}
                </option>
              ))}
            </select>
          </Field>
        )}
        {linkType === 'Url' && (
          <Field label={t('webAddress')}>
            <input
              type="url"
              required
              maxLength={500}
              dir="ltr"
              placeholder="https://"
              value={linkTarget}
              onChange={(e) => setLinkTarget(e.target.value)}
            />
          </Field>
        )}
      </div>

      <label className="check-row">
        <input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} />
        {t('bannerActive')}
      </label>

      <fieldset className="mg-fieldset">
        <legend>{t('bannerImage')}</legend>
        {banner ? (
          <div className="media-logo">
            {banner.imageUrl ? (
              <img className="banner-preview" src={mediaUrl(banner.imageUrl)} alt="" />
            ) : (
              <span className="banner-preview media-placeholder">
                <ImageOff size={24} aria-hidden="true" />
                <span className="visually-hidden">{t('noImage')}</span>
              </span>
            )}
            <div className="chip-row">
              <UploadButton disabled={busy} onFiles={([file]) => void image(file)}>
                {t('uploadImage')}
              </UploadButton>
              {banner.imageUrl && (
                <Button variant="text" disabled={busy} onClick={() => void image()}>
                  {t('remove')}
                </Button>
              )}
            </div>
          </div>
        ) : (
          <span className="mg-field-hint">{t('saveFirstForImage')}</span>
        )}
      </fieldset>

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

export function MoveButtons({
  index,
  count,
  busy,
  onMove,
}: {
  index: number;
  count: number;
  busy: boolean;
  onMove: (by: -1 | 1) => void;
}) {
  const { t } = useI18n();
  return (
    <>
      <Button variant="secondary" aria-label={t('moveUp')} disabled={busy || index === 0} onClick={() => onMove(-1)}>
        <ChevronUp size={18} aria-hidden="true" />
      </Button>
      <Button variant="secondary" aria-label={t('moveDown')} disabled={busy || index === count - 1} onClick={() => onMove(1)}>
        <ChevronDown size={18} aria-hidden="true" />
      </Button>
    </>
  );
}

/* Featured venues */

function FeaturedCard({ content, change }: { content: AppContent; change: Change }) {
  const { t, name } = useI18n();
  const [ids, setIds] = useState(() => content.featured.map((f) => f.id));
  const [pick, setPick] = useState('');
  const [busy, setBusy] = useState(false);

  const saved = content.featured.map((f) => f.id);
  const dirty = ids.join() !== saved.join();
  const info = (id: string) =>
    content.featured.find((f) => f.id === id) ?? { ...content.venues.find((v) => v.id === id)!, published: true };
  const addable = content.venues.filter((v) => !ids.includes(v.id));

  async function save() {
    setBusy(true);
    try {
      let next: AppContent | undefined;
      const ok = await change(async () => {
        next = await api<AppContent>('/admin/app-content/featured', { method: 'PUT', body: ids });
        return next;
      }, t('saved'));
      if (ok && next) setIds(next.featured.map((f) => f.id));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card">
      <div className="card-head">
        <h2>{t('featuredTitle')}</h2>
        <span className="mg-field-hint">{t('bannerCount', { n: ids.length, max: MAX_FEATURED })}</span>
      </div>
      <p className="mg-field-hint">{t('featuredHint')}</p>
      {ids.length === 0 ? (
        <p className="empty">{t('noFeatured')}</p>
      ) : (
        <ol className="content-list">
          {ids.map((id, index) => {
            const v = info(id);
            return (
              <li key={id} className="content-row">
                <div className="content-row-main">
                  <strong>{name(v)}</strong>
                  {!v.published && (
                    <span className="pill-row">
                      <Pill tone="warning">{t('notPublished')}</Pill>
                    </span>
                  )}
                </div>
                <div className="content-row-actions">
                  <MoveButtons
                    index={index}
                    count={ids.length}
                    busy={busy}
                    onMove={(by) => setIds((list) => swap(list, index, by))}
                  />
                  <Button variant="text" disabled={busy} onClick={() => setIds((list) => list.filter((x) => x !== id))}>
                    {t('remove')}
                  </Button>
                </div>
              </li>
            );
          })}
        </ol>
      )}
      <div className="period-fields">
        <Field label={t('addVenue')}>
          <select value={pick} disabled={ids.length >= MAX_FEATURED} onChange={(e) => setPick(e.target.value)}>
            <option value="">{t('chooseVenue')}</option>
            {addable.map((v) => (
              <option key={v.id} value={v.id}>
                {name(v)}
              </option>
            ))}
          </select>
        </Field>
        <Button
          variant="secondary"
          disabled={!pick}
          disabledReason={ids.length >= MAX_FEATURED ? t('featuredFull') : undefined}
          onClick={() => {
            setIds((list) => [...list, pick]);
            setPick('');
          }}
        >
          {t('addVenue')}
        </Button>
      </div>
      <div className="form-actions">
        {dirty && <span className="legal-unsaved">{t('unsavedChanges')}</span>}
        <Button disabled={busy || !dirty} onClick={() => void save()}>
          {busy ? t('saving') : t('save')}
        </Button>
      </div>
    </section>
  );
}

/* Message all users */

function BroadcastCard({ show }: { show: Show }) {
  const { t, lang } = useI18n();
  const history = useLoad(() => api<Broadcast[]>('/admin/broadcasts'));
  const [titleEn, setTitleEn] = useState('');
  const [titleAr, setTitleAr] = useState('');
  const [bodyEn, setBodyEn] = useState('');
  const [bodyAr, setBodyAr] = useState('');
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);

  async function send() {
    setBusy(true);
    try {
      const sent = await api<Broadcast>('/admin/broadcasts', { method: 'POST', body: { titleEn, titleAr, bodyEn, bodyAr } });
      history.setData([sent, ...(history.data ?? [])]);
      setTitleEn('');
      setTitleAr('');
      setBodyEn('');
      setBodyAr('');
      show('success', t('sentToPhones', { n: sent.delivered }));
    } catch (err) {
      show('error', errorText(err));
    } finally {
      setBusy(false);
      setConfirm(false);
    }
  }

  return (
    <section className="card">
      <h2>{t('broadcastTitle')}</h2>
      <p className="mg-field-hint">{t('broadcastHint')}</p>
      <form
        className="dialog-form"
        onSubmit={(e) => {
          e.preventDefault();
          setConfirm(true);
        }}
      >
        <div className="form-grid">
          <Field label={t('titleEn')}>
            <input required maxLength={80} dir="ltr" lang="en" value={titleEn} onChange={(e) => setTitleEn(e.target.value)} />
          </Field>
          <Field label={t('titleAr')}>
            <input required maxLength={80} dir="rtl" lang="ar" value={titleAr} onChange={(e) => setTitleAr(e.target.value)} />
          </Field>
          <Field label={t('messageEn')}>
            <textarea required rows={3} maxLength={300} dir="ltr" lang="en" value={bodyEn} onChange={(e) => setBodyEn(e.target.value)} />
          </Field>
          <Field label={t('messageAr')}>
            <textarea required rows={3} maxLength={300} dir="rtl" lang="ar" value={bodyAr} onChange={(e) => setBodyAr(e.target.value)} />
          </Field>
        </div>
        <div className="form-actions">
          <Button type="submit" disabled={busy}>
            {busy ? t('sending') : t('send')}
          </Button>
        </div>
      </form>

      <Dialog open={confirm} title={t('sendConfirmTitle')} onClose={() => setConfirm(false)}>
        <div className="dialog-form">
          <p>{t('sendConfirm')}</p>
          <div className="form-actions">
            <Button variant="secondary" onClick={() => setConfirm(false)}>
              {t('cancel')}
            </Button>
            <Button disabled={busy} onClick={() => void send()}>
              {busy ? t('sending') : t('send')}
            </Button>
          </div>
        </div>
      </Dialog>

      <h3 className="media-subhead">{t('broadcastHistory')}</h3>
      {history.error && <Notice tone="error">{history.error}</Notice>}
      {history.data &&
        (history.data.length === 0 ? (
          <p className="empty">{t('noBroadcasts')}</p>
        ) : (
          <table className="mg-table">
            <thead>
              <tr>
                <th>{t('sentColumn')}</th>
                <th>{t('titleColumn')}</th>
                <th>{t('sentByColumn')}</th>
                <th className="numeric">{t('deliveredColumn')}</th>
              </tr>
            </thead>
            <tbody>
              {history.data.map((b) => (
                <tr key={b.id}>
                  <td className="nowrap">{formatUtcDate(b.sentAt, lang)}</td>
                  <td>{lang === 'ar' ? b.titleAr : b.titleEn}</td>
                  <td>{b.sentBy ?? '—'}</td>
                  <td className="numeric">
                    {b.delivered} / {b.devices}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ))}
    </section>
  );
}

/* App version & maintenance */

function VersionCard({ content, change }: { content: AppContent; change: Change }) {
  const { t } = useI18n();
  const [minAndroidBuild, setMinAndroid] = useState(content.minAndroidBuild);
  const [minIosBuild, setMinIos] = useState(content.minIosBuild);
  const [androidStoreUrl, setAndroidUrl] = useState(content.androidStoreUrl ?? '');
  const [iosStoreUrl, setIosUrl] = useState(content.iosStoreUrl ?? '');
  const [maintenance, setMaintenance] = useState(content.maintenance);
  const [maintenanceMessageEn, setMessageEn] = useState(content.maintenanceMessageEn ?? '');
  const [maintenanceMessageAr, setMessageAr] = useState(content.maintenanceMessageAr ?? '');
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true);
    try {
      await change(
        () =>
          api<AppContent>('/admin/app-content', {
            method: 'PUT',
            body: {
              minAndroidBuild,
              minIosBuild,
              androidStoreUrl,
              iosStoreUrl,
              maintenance,
              maintenanceMessageEn,
              maintenanceMessageAr,
            },
          }),
        t('saved'),
      );
    } finally {
      setBusy(false);
      setConfirm(false);
    }
  }

  function submit(e: FormEvent) {
    e.preventDefault();
    // Switching maintenance on takes the whole app away from users: ask first.
    if (maintenance && !content.maintenance) setConfirm(true);
    else void save();
  }

  return (
    <section className="card">
      <h2>{t('versionTitle')}</h2>
      <form className="dialog-form" onSubmit={submit}>
        <div className="form-grid">
          <Field label={t('minAndroidBuild')} hint={t('minBuildHint')}>
            <input
              type="number"
              required
              min={1}
              max={100000}
              value={minAndroidBuild}
              onChange={(e) => setMinAndroid(Number(e.target.value))}
            />
          </Field>
          <Field label={t('minIosBuild')} hint={t('minBuildHint')}>
            <input
              type="number"
              required
              min={1}
              max={100000}
              value={minIosBuild}
              onChange={(e) => setMinIos(Number(e.target.value))}
            />
          </Field>
          <Field label={t('androidStoreUrl')}>
            <input type="url" dir="ltr" placeholder="https://" value={androidStoreUrl} onChange={(e) => setAndroidUrl(e.target.value)} />
          </Field>
          <Field label={t('iosStoreUrl')}>
            <input type="url" dir="ltr" placeholder="https://" value={iosStoreUrl} onChange={(e) => setIosUrl(e.target.value)} />
          </Field>
        </div>
        <label className="check-row">
          <input type="checkbox" checked={maintenance} onChange={(e) => setMaintenance(e.target.checked)} />
          {t('maintenanceMode')}
        </label>
        <div className="form-grid">
          <Field label={t('maintenanceEn')}>
            <textarea
              rows={3}
              maxLength={300}
              dir="ltr"
              lang="en"
              value={maintenanceMessageEn}
              onChange={(e) => setMessageEn(e.target.value)}
            />
          </Field>
          <Field label={t('maintenanceAr')}>
            <textarea
              rows={3}
              maxLength={300}
              dir="rtl"
              lang="ar"
              value={maintenanceMessageAr}
              onChange={(e) => setMessageAr(e.target.value)}
            />
          </Field>
        </div>
        <div className="form-actions">
          <Button type="submit" disabled={busy}>
            {busy ? t('saving') : t('save')}
          </Button>
        </div>
      </form>

      <Dialog open={confirm} title={t('maintenanceOnTitle')} onClose={() => setConfirm(false)}>
        <div className="dialog-form">
          <p>{t('maintenanceOnConfirm')}</p>
          <div className="form-actions">
            <Button variant="secondary" onClick={() => setConfirm(false)}>
              {t('cancel')}
            </Button>
            <Button variant="destructive" disabled={busy} onClick={() => void save()}>
              {t('maintenanceOnSave')}
            </Button>
          </div>
        </div>
      </Dialog>
    </section>
  );
}
