import { useEffect, useState } from 'react';
import { Notice } from '../../components/Notice';
import { PageHeader } from '../../components/PageHeader';
import { api, ApiError, errorText } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import { MISSING_KEY, type Category, type CourtInput, type Hours, type MissingItem, type Venue } from '../../lib/types';
import { useLoad } from '../../lib/useLoad';
import type { MessageKey } from '../../locales/en';
import { CourtsSection } from './CourtsSection';
import { DetailsSection, type DetailsInput } from './DetailsSection';
import { HoursSection } from './HoursSection';
import { PublishPanel } from './PublishPanel';

type Toast = { tone: 'error' | 'success'; text: string };

export function VenuePage({ canEdit }: { canEdit: boolean }) {
  const { t, name } = useI18n();
  const venue = useLoad(() => api<Venue>('/venue'));
  const categories = useLoad(() => api<Category[]>('/categories'));
  const [toast, setToast] = useState<Toast>();

  // Success messages fade after a few seconds; errors stay until the next action.
  useEffect(() => {
    if (toast?.tone !== 'success') return;
    const timer = setTimeout(() => setToast(undefined), 5000);
    return () => clearTimeout(timer);
  }, [toast]);

  async function run(action: () => Promise<Venue>, success: MessageKey) {
    setToast(undefined);
    try {
      venue.setData(await action());
      setToast({ tone: 'success', text: t(success) });
    } catch (err) {
      const missing =
        err instanceof ApiError && err.missing
          ? ` ${err.missing.map((m) => t(MISSING_KEY[m as MissingItem])).join(', ')}`
          : '';
      setToast({ tone: 'error', text: errorText(err) + missing });
    }
  }

  async function saveCourt(courtId: string | null, input: CourtInput) {
    const updated = await api<Venue>(courtId ? `/venue/courts/${courtId}` : '/venue/courts', {
      method: courtId ? 'PUT' : 'POST',
      body: input,
    });
    venue.setData(updated);
    setToast({ tone: 'success', text: t('saved') });
  }

  if (venue.error) {
    return (
      <div className="page">
        <Notice tone="error">{venue.error}</Notice>
      </div>
    );
  }
  if (!venue.data || !categories.data) return <p className="page-loading">{t('loading')}</p>;
  const current = venue.data;

  return (
    <div className="page">
      <PageHeader title={name(current)} />
      {!canEdit && <Notice tone="info">{t('readOnlyVenue')}</Notice>}
      <PublishPanel
        venue={current}
        canEdit={canEdit}
        onPublish={() => void run(() => api<Venue>('/venue/publish', { method: 'POST' }), 'publishedNotice')}
        onUnpublish={() => void run(() => api<Venue>('/venue/unpublish', { method: 'POST' }), 'unpublishedNotice')}
      />
      <DetailsSection
        venue={current}
        canEdit={canEdit}
        onSave={(input: DetailsInput) => run(() => api<Venue>('/venue', { method: 'PUT', body: input }), 'saved')}
      />
      <HoursSection
        venue={current}
        canEdit={canEdit}
        onSave={(hours: Hours[]) => run(() => api<Venue>('/venue/hours', { method: 'PUT', body: hours }), 'saved')}
      />
      <CourtsSection venue={current} categories={categories.data} canEdit={canEdit} onSave={saveCourt} />
      <div className="toast-region" aria-live="polite">
        {toast && <Notice tone={toast.tone}>{toast.text}</Notice>}
      </div>
    </div>
  );
}
