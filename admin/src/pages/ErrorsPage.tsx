import { useEffect, useState } from 'react';
import { ListToolbar } from '../components/ListToolbar';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pager } from '../components/Pager';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { ErrorEntry, Paged } from '../lib/types';
import { useLoad } from '../lib/useLoad';

const PAGE_SIZE = 50;

/** Unexpected server errors from the last 30 days, newest first, each with its full detail. */
export function ErrorsPage() {
  const { t } = useI18n();
  const [term, setTerm] = useState('');
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [count, setCount] = useState<Count>({ shown: 0, total: 0 });

  useEffect(() => {
    const timer = setTimeout(() => {
      setQ(term.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [term]);

  return (
    <div className="page">
      <PageHeader title={t('navErrors')} />
      <p className="page-intro">{t('errorsIntro')}</p>
      {/* The box stays mounted while results reload, so typing never loses focus. */}
      <ListToolbar q={term} onQ={setTerm} placeholder={t('searchErrors')} shown={count.shown} total={count.total} />
      <ErrorsTable key={`${q}|${page}`} q={q} page={page} setPage={setPage} onCount={setCount} />
    </div>
  );
}

type Count = { shown: number; total: number };

function ErrorsTable(props: { q: string; page: number; setPage: (p: number) => void; onCount: (c: Count) => void }) {
  const { t, lang } = useI18n();
  const list = useLoad(() =>
    api<Paged<ErrorEntry>>(`/admin/errors?page=${props.page}${props.q ? `&q=${encodeURIComponent(props.q)}` : ''}`),
  );
  const when = new Intl.DateTimeFormat(lang === 'ar' ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB', { dateStyle: 'medium', timeStyle: 'medium' });
  const { onCount } = props;
  useEffect(() => {
    if (list.data) onCount({ shown: list.data.items.length, total: list.data.total });
  }, [list.data, onCount]);

  return (
    <>
      {list.error && <Notice tone="error">{list.error}</Notice>}
      {!list.data && !list.error && <p className="page-loading">{t('loading')}</p>}
      {list.data?.items.length === 0 && (
        <section className="card">
          <p className="empty">{t(props.q ? 'noMatches' : 'noErrors')}</p>
        </section>
      )}
      {list.data && list.data.items.length > 0 && (
        <div className="error-list">
          {list.data.items.map((e) => (
            <details key={e.id} className="card error-item">
              <summary>
                <span className="error-when">{when.format(new Date(e.at))}</span>
                <span className="error-what">
                  <strong>{e.message}</strong>
                  <small dir="ltr">
                    <span className={`method method--${e.method.toLowerCase()}`}>{e.method}</span> {e.path}
                  </small>
                </span>
                <span className="muted">{e.staffName ?? t('notSignedIn')}</span>
              </summary>
              <dl className="details">
                <dt>{t('errorType')}</dt>
                <dd>
                  <code dir="ltr">{e.exceptionType}</code>
                </dd>
                <dt>{t('traceId')}</dt>
                <dd>
                  <code dir="ltr">{e.traceId}</code>
                </dd>
              </dl>
              <pre className="error-detail" dir="ltr">
                {e.detail}
              </pre>
            </details>
          ))}
        </div>
      )}
      {list.data && <Pager page={props.page} total={list.data.total} size={PAGE_SIZE} onPage={props.setPage} />}
    </>
  );
}
