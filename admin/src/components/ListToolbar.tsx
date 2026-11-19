import { useId } from 'react';
import { useSearchParams } from 'react-router';
import { Search } from 'lucide-react';
import { useI18n } from '../lib/i18n';
import type { MessageKey } from '../locales/en';

/** Case-insensitive "contains", for filtering lists already loaded in the page. */
export const matches = (text: string, q: string) => text.toLocaleLowerCase().includes(q.trim().toLocaleLowerCase());

/** The search a page opens with: `?q=` from the top-bar search, else empty. */
export function useInitialQuery() {
  const [params] = useSearchParams();
  return params.get('q') ?? '';
}

/** Search box + filter chips above a table, Zenith-style. Filtering happens in the page; this only holds the controls. */
export function ListToolbar<F extends string>(props: {
  q: string;
  onQ: (q: string) => void;
  placeholder: string;
  filters?: [F, MessageKey][];
  filter?: F;
  onFilter?: (f: F) => void;
  shown: number;
  total: number;
}) {
  const { t } = useI18n();
  const group = useId();
  return (
    <div className="list-toolbar">
      <label className="toolbar-search">
        <Search aria-hidden size={16} />
        <input
          type="search"
          aria-label={props.placeholder}
          placeholder={props.placeholder}
          value={props.q}
          onChange={(e) => props.onQ(e.target.value)}
        />
      </label>
      {props.filters && (
        <div className="chip-row" role="radiogroup" aria-label={t('filterBy')}>
          {props.filters.map(([value, label]) => (
            <label key={value} className="check-chip">
              <input type="radio" name={group} checked={props.filter === value} onChange={() => props.onFilter?.(value)} />
              {t(label)}
            </label>
          ))}
        </div>
      )}
      <span className="toolbar-count" aria-live="polite">
        {t('showingNofM', { n: props.shown, m: props.total })}
      </span>
    </div>
  );
}
