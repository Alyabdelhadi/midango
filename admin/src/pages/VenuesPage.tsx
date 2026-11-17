import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { VenueSummary } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { StaffForm } from './StaffForm';

export function VenuesPage() {
  const { t, name } = useI18n();
  const venues = useLoad(() => api<VenueSummary[]>('/admin/venues'));
  const [creating, setCreating] = useState(false);
  const [managerFor, setManagerFor] = useState<VenueSummary | null>(null);
  const [notice, setNotice] = useState<string>();
  const list = venues.data;

  return (
    <div className="page">
      <PageHeader title={t('navVenues')} action={<Button onClick={() => setCreating(true)}>{t('newVenue')}</Button>} />
      {notice && <Notice tone="success">{notice}</Notice>}
      {venues.error && <Notice tone="error">{venues.error}</Notice>}
      {list?.length === 0 && <p className="empty">{t('noVenues')}</p>}
      {list && list.length > 0 && (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('nameEn')}</th>
              <th>{t('nameAr')}</th>
              <th>
                <span className="visually-hidden">{t('actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {list.map((venue) => (
              <tr key={venue.id}>
                <td>{venue.nameEn}</td>
                <td dir="rtl" lang="ar">
                  {venue.nameAr}
                </td>
                <td className="row-actions">
                  <Button variant="text" onClick={() => setManagerFor(venue)}>
                    {t('addManager')}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <Dialog open={creating} title={t('newVenue')} onClose={() => setCreating(false)}>
        <NewVenueForm
          onCancel={() => setCreating(false)}
          onSaved={() => {
            setCreating(false);
            venues.reload();
          }}
        />
      </Dialog>

      <Dialog
        open={managerFor !== null}
        title={t('addManagerTo', { venue: managerFor ? name(managerFor) : '' })}
        onClose={() => setManagerFor(null)}
      >
        {managerFor && (
          <StaffForm
            path={`/admin/venues/${managerFor.id}/staff`}
            role="VenueManager"
            onCancel={() => setManagerFor(null)}
            onSaved={() => {
              setManagerFor(null);
              setNotice(t('managerAdded'));
            }}
          />
        )}
      </Dialog>
    </div>
  );
}

function NewVenueForm({ onCancel, onSaved }: { onCancel: () => void; onSaved: () => void }) {
  const { t } = useI18n();
  const [nameEn, setNameEn] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api('/admin/venues', { method: 'POST', body: { nameEn, nameAr } });
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
      <Field label={t('nameEn')}>
        <input required maxLength={120} value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
      </Field>
      <Field label={t('nameAr')}>
        <input required maxLength={120} dir="rtl" lang="ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
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
