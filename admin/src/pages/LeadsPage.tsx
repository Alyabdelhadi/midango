import { useState } from 'react';
import { Button } from '../components/Button';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { VenueLead } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';

export function LeadsPage() {
  const { t, lang } = useI18n();
  const toast = useToast();
  const leads = useLoad(() => api<VenueLead[]>('/admin/leads'));
  const [busyId, setBusyId] = useState<string | null>(null);

  async function toggle(lead: VenueLead) {
    setBusyId(lead.id);
    try {
      const updated = await api<VenueLead>(`/admin/leads/${lead.id}/contacted`, {
        method: 'PUT',
        body: { contacted: !lead.contactedAt },
      });
      leads.setData((list) => list?.map((l) => (l.id === updated.id ? updated : l)));
    } catch (err) {
      toast.show('error', errorText(err));
    } finally {
      setBusyId(null);
    }
  }

  // createdAt is UTC; Intl shows it in the viewer's local time.
  const received = new Intl.DateTimeFormat(lang === 'ar' ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

  return (
    <div className="page">
      <PageHeader title={t('navLeads')} />
      <p className="page-intro">{t('leadsIntro')}</p>
      {leads.error ? (
        <Notice tone="error">{leads.error}</Notice>
      ) : !leads.data ? (
        <p className="page-loading">{t('loading')}</p>
      ) : leads.data.length === 0 ? (
        <p className="empty">{t('noLeads')}</p>
      ) : (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('leadReceived')}</th>
              <th>{t('leadVenue')}</th>
              <th>{t('leadContact')}</th>
              <th className="numeric">{t('leadCourts')}</th>
              <th>{t('leadMessage')}</th>
              <th>{t('status')}</th>
            </tr>
          </thead>
          <tbody>
            {leads.data.map((l) => (
              <tr key={l.id}>
                <td>{received.format(new Date(l.createdAt))}</td>
                <td>
                  {l.venueName}
                  <div className="muted">{l.city}</div>
                </td>
                <td>
                  {l.contactName}
                  <div>
                    <a href={`tel:${l.phone}`}>
                      <bdi dir="ltr">{l.phone}</bdi>
                    </a>
                  </div>
                </td>
                <td className="numeric">{l.courts}</td>
                <td className="lead-message">{l.message || '—'}</td>
                <td>
                  <Button variant="secondary" disabled={busyId === l.id} onClick={() => toggle(l)}>
                    {l.contactedAt ? t('contacted') : t('markContacted')}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {toast.view}
    </div>
  );
}
