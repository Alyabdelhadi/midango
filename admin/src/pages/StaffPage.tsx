import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api, errorText } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { ROLE_KEY, type StaffSummary } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import { StaffForm } from './StaffForm';

export function StaffPage() {
  const { t } = useI18n();
  const { staff: me, acting } = useAuth();
  const staff = useLoad(() => api<StaffSummary[]>('/venue/staff'));
  const toast = useToast();
  const [adding, setAdding] = useState(false);
  const [resetting, setResetting] = useState<StaffSummary | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string>();
  const list = staff.data;

  // Never yourself; a manager changes desk staff only, an operator inside the venue anyone.
  const canManage = (member: StaffSummary) => member.id !== me?.id && (acting !== null || member.role === 'DeskStaff');

  async function setActive(member: StaffSummary, active: boolean) {
    setBusyId(member.id);
    try {
      const updated = await api<StaffSummary>(`/venue/staff/${member.id}/active`, { method: 'PUT', body: { active } });
      staff.setData((all) => all?.map((m) => (m.id === updated.id ? updated : m)));
    } catch (err) {
      toast.show('error', errorText(err));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="page">
      <PageHeader title={t('navStaff')} action={<Button onClick={() => setAdding(true)}>{t('addDeskStaff')}</Button>} />
      {notice && <Notice tone="success">{notice}</Notice>}
      {staff.error && <Notice tone="error">{staff.error}</Notice>}
      {list?.length === 0 && <p className="empty">{t('noStaff')}</p>}
      {list && list.length > 0 && (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('displayName')}</th>
              <th>{t('email')}</th>
              <th>{t('role')}</th>
              <th>{t('status')}</th>
              <th>
                <span className="visually-hidden">{t('actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {list.map((member) => (
              <tr key={member.id}>
                <td>{member.displayName}</td>
                <td dir="ltr">{member.email}</td>
                <td>{t(ROLE_KEY[member.role])}</td>
                <td>
                  <Pill tone={member.active ? 'success' : 'neutral'}>{member.active ? t('active') : t('inactive')}</Pill>
                </td>
                <td className="row-actions">
                  {canManage(member) && (
                    <>
                      <Button
                        variant="text"
                        disabled={busyId === member.id}
                        onClick={() => void setActive(member, !member.active)}
                      >
                        {member.active ? t('deactivate') : t('reactivate')}
                      </Button>
                      <Button variant="text" onClick={() => setResetting(member)}>
                        {t('resetPassword')}
                      </Button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Dialog open={adding} title={t('addDeskStaff')} onClose={() => setAdding(false)}>
        <StaffForm
          path="/venue/staff"
          onCancel={() => setAdding(false)}
          onSaved={() => {
            setAdding(false);
            setNotice(t('staffAdded'));
            staff.reload();
          }}
        />
      </Dialog>
      <Dialog
        open={resetting !== null}
        title={t('resetPasswordFor', { name: resetting?.displayName ?? '' })}
        onClose={() => setResetting(null)}
      >
        {resetting && (
          <ResetPasswordForm
            member={resetting}
            onCancel={() => setResetting(null)}
            onSaved={() => {
              setResetting(null);
              toast.show('success', t('passwordReset'));
            }}
          />
        )}
      </Dialog>
      {toast.view}
    </div>
  );
}

function ResetPasswordForm({
  member,
  onCancel,
  onSaved,
}: {
  member: StaffSummary;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const { t } = useI18n();
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api(`/venue/staff/${member.id}/reset-password`, { method: 'POST', body: { temporaryPassword } });
      onSaved();
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={submit}>
      {error && <Notice tone="error">{error}</Notice>}
      <Field label={t('temporaryPassword')} hint={t('temporaryPasswordHint')}>
        <input
          required
          minLength={10}
          autoComplete="new-password"
          value={temporaryPassword}
          onChange={(e) => setTemporaryPassword(e.target.value)}
        />
      </Field>
      <div className="form-actions">
        <Button variant="secondary" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t('resetPassword')}
        </Button>
      </div>
    </form>
  );
}
