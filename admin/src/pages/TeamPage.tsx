import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { ListToolbar, matches } from '../components/ListToolbar';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { initials } from '../layout/nav';
import { api, errorText } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { OP_ROLE_KEY, OPERATOR_ROLES, type OperatorRole, type TeamMember } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useToast } from '../lib/useToast';
import type { MessageKey } from '../locales/en';

const ROLE_HELP: Record<OperatorRole, MessageKey> = {
  Owner: 'opRoleOwnerHelp',
  Finance: 'opRoleFinanceHelp',
  Support: 'opRoleSupportHelp',
  Content: 'opRoleContentHelp',
};

/** The Midango team: who can sign in to this admin and what each role may open. */
export function TeamPage() {
  const { t } = useI18n();
  const { staff: me } = useAuth();
  const toast = useToast();
  const team = useLoad(() => api<TeamMember[]>('/admin/team'));
  const [q, setQ] = useState('');
  const [adding, setAdding] = useState(false);
  const [resetting, setResetting] = useState<TeamMember | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string>();
  const list = team.data;
  const shown = list?.filter((m) => matches(`${m.displayName} ${m.email}`, q));

  async function update(member: TeamMember, body: { role?: OperatorRole; active?: boolean }) {
    setBusyId(member.id);
    try {
      const updated = await api<TeamMember>(`/admin/team/${member.id}`, { method: 'PUT', body });
      team.setData((all) => all?.map((m) => (m.id === updated.id ? updated : m)));
      toast.show('success', t('teamUpdated', { name: updated.displayName }));
    } catch (err) {
      toast.show('error', errorText(err));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="page">
      <PageHeader title={t('navTeam')} action={<Button onClick={() => setAdding(true)}>{t('addTeamMember')}</Button>} />
      <p className="page-intro">{t('teamIntro')}</p>
      {notice && <Notice tone="success">{notice}</Notice>}
      {team.error && <Notice tone="error">{team.error}</Notice>}
      {!list && !team.error && <p className="page-loading">{t('loading')}</p>}
      {list && (
        <>
          <ListToolbar q={q} onQ={setQ} placeholder={t('searchStaff')} shown={shown!.length} total={list.length} />
          <table className="mg-table">
            <thead>
              <tr>
                <th scope="col">{t('teamMember')}</th>
                <th scope="col">{t('role')}</th>
                <th scope="col">{t('status')}</th>
                <th scope="col">
                  <span className="visually-hidden">{t('actions')}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {shown!.map((m) => {
                const self = m.id === me?.id;
                return (
                  <tr key={m.id}>
                    <th scope="row">
                      <span className="who">
                        <span className="avatar" aria-hidden="true">
                          {initials(m.displayName)}
                        </span>
                        <span>
                          {m.displayName} {self && <span className="muted">({t('you')})</span>}
                          <small>{m.email}</small>
                        </span>
                      </span>
                    </th>
                    <td>
                      <select
                        aria-label={t('roleFor', { name: m.displayName })}
                        value={m.role}
                        disabled={self || busyId === m.id}
                        onChange={(e) => void update(m, { role: e.target.value as OperatorRole })}
                      >
                        {OPERATOR_ROLES.map((r) => (
                          <option key={r} value={r}>
                            {t(OP_ROLE_KEY[r])}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      {!m.active ? (
                        <Pill>{t('inactive')}</Pill>
                      ) : m.mustChangePassword ? (
                        <Pill tone="warning">{t('awaitingFirstSignIn')}</Pill>
                      ) : (
                        <Pill tone="success">{t('active')}</Pill>
                      )}
                    </td>
                    <td className="row-actions">
                      {!self && (
                        <>
                          <Button variant="text" disabled={busyId === m.id} onClick={() => void update(m, { active: !m.active })}>
                            {t(m.active ? 'deactivate' : 'activate')}
                          </Button>
                          <Button variant="text" onClick={() => setResetting(m)}>
                            {t('resetPassword')}
                          </Button>
                        </>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </>
      )}

      <section className="card">
        <h2>{t('rolesTitle')}</h2>
        <dl className="details">
          {OPERATOR_ROLES.map((r) => (
            <div key={r} style={{ display: 'contents' }}>
              <dt>{t(OP_ROLE_KEY[r])}</dt>
              <dd>{t(ROLE_HELP[r])}</dd>
            </div>
          ))}
        </dl>
      </section>

      <Dialog open={adding} title={t('addTeamMember')} onClose={() => setAdding(false)}>
        <AddMemberForm
          onCancel={() => setAdding(false)}
          onSaved={(m) => {
            setAdding(false);
            team.setData((all) => [...(all ?? []), m].sort((a, b) => a.displayName.localeCompare(b.displayName)));
            setNotice(t('teamMemberAdded', { name: m.displayName }));
          }}
        />
      </Dialog>
      <Dialog open={resetting !== null} title={t('resetPasswordFor', { name: resetting?.displayName ?? '' })} onClose={() => setResetting(null)}>
        {resetting && (
          <ResetForm
            member={resetting}
            onCancel={() => setResetting(null)}
            onSaved={() => {
              setResetting(null);
              setNotice(t('passwordReset'));
            }}
          />
        )}
      </Dialog>
    </div>
  );
}

function AddMemberForm({ onCancel, onSaved }: { onCancel: () => void; onSaved: (m: TeamMember) => void }) {
  const { t } = useI18n();
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<OperatorRole>('Support');
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      onSaved(await api<TeamMember>('/admin/team', { method: 'POST', body: { displayName, email, role, temporaryPassword } }));
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={save}>
      {error && <Notice tone="error">{error}</Notice>}
      <Field label={t('displayName')}>
        <input required maxLength={80} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
      </Field>
      <Field label={t('email')}>
        <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
      </Field>
      <Field label={t('role')} hint={t(ROLE_HELP[role])}>
        <select value={role} onChange={(e) => setRole(e.target.value as OperatorRole)}>
          {OPERATOR_ROLES.map((r) => (
            <option key={r} value={r}>
              {t(OP_ROLE_KEY[r])}
            </option>
          ))}
        </select>
      </Field>
      <Field label={t('temporaryPassword')} hint={t('temporaryPasswordHint')}>
        <input required minLength={10} value={temporaryPassword} onChange={(e) => setTemporaryPassword(e.target.value)} />
      </Field>
      <div className="form-actions">
        <Button variant="secondary" type="button" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t('save')}
        </Button>
      </div>
    </form>
  );
}

function ResetForm({ member, onCancel, onSaved }: { member: TeamMember; onCancel: () => void; onSaved: () => void }) {
  const { t } = useI18n();
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api(`/admin/team/${member.id}/reset-password`, { method: 'POST', body: { temporaryPassword } });
      onSaved();
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={save}>
      {error && <Notice tone="error">{error}</Notice>}
      <Field label={t('temporaryPassword')} hint={t('temporaryPasswordHint')}>
        <input required minLength={10} value={temporaryPassword} onChange={(e) => setTemporaryPassword(e.target.value)} />
      </Field>
      <div className="form-actions">
        <Button variant="secondary" type="button" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t('resetPassword')}
        </Button>
      </div>
    </form>
  );
}
