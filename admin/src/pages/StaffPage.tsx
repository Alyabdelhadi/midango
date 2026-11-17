import { useState } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { ROLE_KEY, type StaffSummary } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { StaffForm } from './StaffForm';

export function StaffPage() {
  const { t } = useI18n();
  const staff = useLoad(() => api<StaffSummary[]>('/venue/staff'));
  const [adding, setAdding] = useState(false);
  const [notice, setNotice] = useState<string>();
  const list = staff.data;

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
    </div>
  );
}
