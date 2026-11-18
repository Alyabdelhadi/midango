import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { clock, dayOf, formatDay } from '../lib/time';
import type { VenueCustomer } from '../lib/types';
import { useLoad } from '../lib/useLoad';

export function CustomersPage() {
  const { t } = useI18n();
  const [term, setTerm] = useState('');
  const [q, setQ] = useState('');

  function search(e: FormEvent) {
    e.preventDefault();
    setQ(term.trim());
  }

  return (
    <div className="page">
      <PageHeader title={t('navCustomers')} />
      <p className="page-intro">{t('customersIntro')}</p>
      <form className="search-form" onSubmit={search} role="search">
        <Field label={t('search')} hint={t('searchCustomersHint')}>
          <input type="search" value={term} onChange={(e) => setTerm(e.target.value)} />
        </Field>
        <Button type="submit" variant="secondary">
          {t('search')}
        </Button>
      </form>
      <CustomersTable key={q} q={q} />
    </div>
  );
}

function CustomersTable({ q }: { q: string }) {
  const { t, lang } = useI18n();
  const list = useLoad(() => api<VenueCustomer[]>(`/venue/customers${q ? `?q=${encodeURIComponent(q)}` : ''}`));

  if (list.error) return <Notice tone="error">{list.error}</Notice>;
  if (!list.data) return <p className="page-loading">{t('loading')}</p>;
  if (list.data.length === 0) return <p className="empty">{t('noCustomers')}</p>;

  return (
    <table className="mg-table">
      <thead>
        <tr>
          <th>{t('customer')}</th>
          <th>{t('phone')}</th>
          <th className="numeric">{t('navBookings')}</th>
          <th className="numeric">{t('cancellations')}</th>
          <th>{t('lastBooking')}</th>
        </tr>
      </thead>
      <tbody>
        {list.data.map((c) => (
          <tr key={c.id}>
            <td>{c.name ?? <span className="muted">{t('noName')}</span>}</td>
            <td>
              <bdi dir="ltr">{c.phoneMasked}</bdi>
            </td>
            <td className="numeric">{c.bookings}</td>
            <td className="numeric">{c.cancellations}</td>
            <td>
              {formatDay(dayOf(c.lastBookingAt), lang, 'short')} · {clock(c.lastBookingAt)}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
