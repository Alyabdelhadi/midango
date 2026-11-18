import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api, errorText } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import type { OperatorAction, VenueSummary } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import { StaffForm } from './StaffForm';

export function VenuesPage() {
  const { t, name } = useI18n();
  const venues = useLoad(() => api<VenueSummary[]>('/admin/venues'));
  const [creating, setCreating] = useState(false);
  const [managerFor, setManagerFor] = useState<VenueSummary | null>(null);
  const [notice, setNotice] = useState<string>();
  const [suspending, setSuspending] = useState<VenueSummary | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const { openVenue } = useAuth();
  const navigate = useNavigate();
  const toast = useToast();
  const list = venues.data;

  async function enterVenue(venue: VenueSummary) {
    setBusyId(venue.id);
    try {
      await openVenue(venue.id);
      navigate('/today');
    } catch (err) {
      toast.show('error', errorText(err));
      setBusyId(null);
    }
  }

  async function setSuspended(venue: VenueSummary, suspend: boolean) {
    setBusyId(venue.id);
    try {
      const updated = await api<VenueSummary>(`/admin/venues/${venue.id}/${suspend ? 'suspend' : 'unsuspend'}`, {
        method: 'POST',
        body: {},
      });
      venues.setData((all) => all?.map((v) => (v.id === updated.id ? updated : v)));
      toast.show('success', t(suspend ? 'venueSuspended' : 'venueUnsuspended', { venue: name(venue) }));
    } catch (err) {
      toast.show('error', errorText(err));
    } finally {
      setBusyId(null);
      setSuspending(null);
    }
  }

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
              <th>{t('status')}</th>
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
                <td>
                  <VenueStatus venue={venue} />
                  <OperatorLog venueId={venue.id} />
                </td>
                <td className="row-actions">
                  <Button
                    variant="secondary"
                    disabled={busyId === venue.id}
                    disabledReason={venue.suspended ? t('openSuspendedReason') : undefined}
                    onClick={() => void enterVenue(venue)}
                  >
                    {t('openVenue')}
                  </Button>
                  {venue.suspended ? (
                    <Button variant="text" disabled={busyId === venue.id} onClick={() => void setSuspended(venue, false)}>
                      {t('unsuspend')}
                    </Button>
                  ) : (
                    <Button variant="text" disabled={busyId === venue.id} onClick={() => setSuspending(venue)}>
                      {t('suspend')}
                    </Button>
                  )}
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

      <Dialog
        open={suspending !== null}
        title={t('suspendTitle', { venue: suspending ? name(suspending) : '' })}
        onClose={() => setSuspending(null)}
      >
        {suspending && (
          <div className="dialog-form">
            <p>{t('suspendConfirm')}</p>
            <div className="form-actions">
              <Button variant="secondary" onClick={() => setSuspending(null)}>
                {t('cancel')}
              </Button>
              <Button
                variant="destructive"
                disabled={busyId === suspending.id}
                onClick={() => void setSuspended(suspending, true)}
              >
                {t('suspend')}
              </Button>
            </div>
          </div>
        )}
      </Dialog>
      {toast.view}
    </div>
  );
}

function VenueStatus({ venue }: { venue: VenueSummary }) {
  const { t } = useI18n();
  if (venue.suspended) return <Pill tone="warning">{t('venueSuspendedStatus')}</Pill>;
  return venue.published ? <Pill tone="success">{t('venuePublishedStatus')}</Pill> : <Pill>{t('venueDraftStatus')}</Pill>;
}

/** The latest changes operators made inside this venue, fetched the first time it is opened. */
function OperatorLog({ venueId }: { venueId: string }) {
  const { t, lang } = useI18n();
  const [actions, setActions] = useState<OperatorAction[]>();
  const [error, setError] = useState<string>();

  function load() {
    if (actions) return;
    api<OperatorAction[]>(`/admin/venues/${venueId}/actions`).then(setActions, (err: unknown) => setError(errorText(err)));
  }

  // `at` is UTC; Intl shows it in the viewer's local time.
  const when = new Intl.DateTimeFormat(lang === 'ar' ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

  return (
    <details className="operator-log" onToggle={(e) => e.currentTarget.open && load()}>
      <summary>{t('operatorChanges')}</summary>
      {error ? (
        <Notice tone="error">{error}</Notice>
      ) : !actions ? (
        <p className="muted">{t('loading')}</p>
      ) : actions.length === 0 ? (
        <p className="muted">{t('noOperatorChanges')}</p>
      ) : (
        <ul>
          {actions.map((a, i) => (
            <li key={i}>
              {when.format(new Date(a.at))} · {a.operatorName} ·{' '}
              <bdi dir="ltr">
                {a.method} {a.path} → {a.status}
              </bdi>
            </li>
          ))}
        </ul>
      )}
    </details>
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
