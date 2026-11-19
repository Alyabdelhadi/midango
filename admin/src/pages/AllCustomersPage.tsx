import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { formatUtcDate } from '../lib/time';
import type { AdminCustomerList } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { useInitialQuery } from '../components/ListToolbar';

const PAGE_SIZE = 50;

/** Operator: every app customer across venues. The venue-scoped page lives at /customers. */
export function AllCustomersPage() {
  const { t } = useI18n();
  const initial = useInitialQuery();
  const [term, setTerm] = useState(initial);
  const [q, setQ] = useState(initial);
  const [page, setPage] = useState(1);

  function search(e: FormEvent) {
    e.preventDefault();
    setQ(term.trim());
    setPage(1);
  }

  return (
    <div className="page">
      <PageHeader title={t('navCustomers')} />
      <p className="page-intro">{t('allCustomersIntro')}</p>
      <form className="search-form" onSubmit={search} role="search">
        <Field label={t('search')} hint={t('searchAllCustomersHint')}>
          <input type="search" value={term} onChange={(e) => setTerm(e.target.value)} />
        </Field>
        <Button type="submit" variant="secondary">
          {t('search')}
        </Button>
      </form>
      <CustomersTable key={`${q}|${page}`} q={q} page={page} setPage={setPage} />
    </div>
  );
}

function CustomersTable({ q, page, setPage }: { q: string; page: number; setPage: (page: number) => void }) {
  const { t, lang } = useI18n();
  const list = useLoad(() =>
    api<AdminCustomerList>(`/admin/customers?page=${page}${q ? `&q=${encodeURIComponent(q)}` : ''}`),
  );

  if (list.error) return <Notice tone="error">{list.error}</Notice>;
  if (!list.data) return <p className="page-loading">{t('loading')}</p>;
  if (list.data.items.length === 0) return <p className="empty">{t('noCustomersFound')}</p>;
  const pages = Math.max(1, Math.ceil(list.data.total / PAGE_SIZE));

  return (
    <>
      <p className="muted">{t('customersCount', { total: list.data.total })}</p>
      <table className="mg-table">
        <thead>
          <tr>
            <th>{t('customer')}</th>
            <th>{t('phone')}</th>
            <th>{t('joined')}</th>
            <th className="numeric">{t('tileBookings')}</th>
            <th className="numeric">{t('noShowsInWindow')}</th>
            <th>{t('status')}</th>
          </tr>
        </thead>
        <tbody>
          {list.data.items.map((c) => (
            <tr key={c.id}>
              <td>
                <Link to={`/all-customers/${c.id}`}>{c.name ?? t('noName')}</Link>
              </td>
              <td>
                <bdi dir="ltr" className="nowrap">
                  {c.phone}
                </bdi>
              </td>
              <td>{formatUtcDate(c.createdAt, lang)}</td>
              <td className="numeric">{c.bookings}</td>
              <td className="numeric">{c.noShowsInWindow}</td>
              <td>
                <span className="pill-row">
                  {c.blocked && <Pill tone="warning">{t('accountBlocked')}</Pill>}
                  {c.limited && <Pill>{t('accountLimited')}</Pill>}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {pages > 1 && (
        <nav className="pager" aria-label={t('pageOf', { page, pages })}>
          {page > 1 && (
            <Button variant="secondary" onClick={() => setPage(page - 1)}>
              {t('previousPage')}
            </Button>
          )}
          <span>{t('pageOf', { page, pages })}</span>
          {page < pages && (
            <Button variant="secondary" onClick={() => setPage(page + 1)}>
              {t('nextPage')}
            </Button>
          )}
        </nav>
      )}
    </>
  );
}
