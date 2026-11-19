import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { ListToolbar } from '../components/ListToolbar';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pager } from '../components/Pager';
import { Pill } from '../components/Pill';
import { initials } from '../layout/nav';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { AuditEntry, Paged } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import type { MessageKey } from '../locales/en';

const PAGE_SIZE = 50;

/** Plain words for the common changes; anything else shows its path only. First match wins. */
const ACTIONS: [string, RegExp, MessageKey][] = [
  ['PUT', /^\/admin\/accounting\/venues\/[^/]+\/plan$/, 'auditFeePlan'],
  ['PUT', /^\/admin\/accounting\/statements\//, 'auditPaidStatus'],
  ['PUT', /^\/admin\/settings\/billing$/, 'auditCommission'],
  ['PUT', /^\/admin\/settings$/, 'auditRules'],
  ['POST', /^\/admin\/team$/, 'auditTeamAdded'],
  ['PUT', /^\/admin\/team\/[^/]+$/, 'auditTeamChanged'],
  ['POST', /\/reset-password$/, 'auditPasswordReset'],
  ['POST', /^\/admin\/venues$/, 'auditVenueCreated'],
  ['POST', /^\/admin\/venues\/[^/]+\/act$/, 'auditVenueOpened'],
  ['POST', /^\/admin\/venues\/[^/]+\/suspend$/, 'auditVenueSuspended'],
  ['POST', /^\/admin\/venues\/[^/]+\/unsuspend$/, 'auditVenueUnsuspended'],
  ['POST', /^\/admin\/venues\/[^/]+\/staff$/, 'auditStaffAdded'],
  ['*', /^\/admin\/(categories|features)/, 'auditSports'],
  ['*', /^\/admin\/(app-content|broadcasts)/, 'auditAppContent'],
  ['*', /^\/admin\/site-content/, 'auditWebsite'],
  ['*', /^\/admin\/legal/, 'auditLegal'],
  ['*', /^\/admin\/leads/, 'auditLead'],
  ['*', /^\/admin\/customers/, 'auditCustomer'],
  ['*', /^\/venue\/tournaments/, 'auditVenueTournaments'],
  ['*', /^\/venue\/bookings/, 'auditVenueBookings'],
  ['*', /^\/venue\//, 'auditVenueSetup'],
];

function describe(method: string, path: string): MessageKey | null {
  return ACTIONS.find(([m, re]) => (m === '*' || m === method) && re.test(path))?.[2] ?? null;
}

/** Every change a Midango operator made, newest first. Searching asks the server (the log is paged). */
export function AuditPage() {
  const { t } = useI18n();
  const [term, setTerm] = useState('');
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [count, setCount] = useState<Count>({ shown: 0, total: 0 });

  // Search after a pause in typing, from page 1.
  useEffect(() => {
    const timer = setTimeout(() => {
      setQ(term.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [term]);

  return (
    <div className="page">
      <PageHeader title={t('navAudit')} />
      <p className="page-intro">{t('auditIntro')}</p>
      {/* The box stays mounted while results reload, so typing never loses focus. */}
      <ListToolbar q={term} onQ={setTerm} placeholder={t('searchAudit')} shown={count.shown} total={count.total} />
      <AuditTable key={`${q}|${page}`} q={q} page={page} setPage={setPage} onCount={setCount} />
    </div>
  );
}

type Count = { shown: number; total: number };

function AuditTable(props: { q: string; page: number; setPage: (p: number) => void; onCount: (c: Count) => void }) {
  const { t, lang, name } = useI18n();
  const list = useLoad(() =>
    api<Paged<AuditEntry>>(`/admin/audit?page=${props.page}${props.q ? `&q=${encodeURIComponent(props.q)}` : ''}`),
  );
  const when = new Intl.DateTimeFormat(lang === 'ar' ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' });
  const { onCount } = props;
  useEffect(() => {
    if (list.data) onCount({ shown: list.data.items.length, total: list.data.total });
  }, [list.data, onCount]);

  return (
    <>
      {list.error && <Notice tone="error">{list.error}</Notice>}
      {!list.data && !list.error && <p className="page-loading">{t('loading')}</p>}
      {list.data?.items.length === 0 && <p className="empty">{t(props.q ? 'noMatches' : 'noAudit')}</p>}
      {list.data && list.data.items.length > 0 && (
        <table className="mg-table">
          <thead>
            <tr>
              <th scope="col">{t('when')}</th>
              <th scope="col">{t('teamMember')}</th>
              <th scope="col">{t('action')}</th>
              <th scope="col">{t('navVenue')}</th>
              <th scope="col">{t('result')}</th>
            </tr>
          </thead>
          <tbody>
            {list.data.items.map((a) => (
              <tr key={a.id}>
                <td>{when.format(new Date(a.at))}</td>
                <th scope="row">
                  <span className="who">
                    <span className="avatar avatar--sm" aria-hidden="true">
                      {initials(a.operatorName)}
                    </span>
                    {a.operatorName}
                  </span>
                </th>
                <td>
                  <span className="audit-action">
                    {describe(a.method, a.path) && <strong>{t(describe(a.method, a.path)!)}</strong>}
                    <span>
                      <span className={`method method--${a.method.toLowerCase()}`}>{a.method}</span>{' '}
                      <code dir="ltr">{a.path}</code>
                    </span>
                  </span>
                </td>
                <td>
                  {a.venueId && a.venueNameEn ? (
                    <Link to={`/venues?q=${encodeURIComponent(a.venueNameEn)}`}>
                      {name({ nameEn: a.venueNameEn, nameAr: a.venueNameAr ?? a.venueNameEn })}
                    </Link>
                  ) : (
                    <span className="muted">—</span>
                  )}
                </td>
                <td>
                  <Pill tone={a.status < 300 ? 'success' : a.status < 500 ? 'warning' : 'neutral'}>
                    {a.status < 300 ? t('resultDone') : t('resultRefused', { status: a.status })}
                  </Pill>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {list.data && <Pager page={props.page} total={list.data.total} size={PAGE_SIZE} onPage={props.setPage} />}
    </>
  );
}
