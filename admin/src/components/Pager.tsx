import { useI18n } from '../lib/i18n';
import { Button } from './Button';

/** Previous / "Page 2 of 5" / Next under a paged table. Hidden when there's one page. */
export function Pager({ page, total, size, onPage }: { page: number; total: number; size: number; onPage: (page: number) => void }) {
  const { t } = useI18n();
  const pages = Math.max(1, Math.ceil(total / size));
  if (pages <= 1) return null;
  return (
    <nav className="pager" aria-label={t('pageOf', { page, pages })}>
      {page > 1 && (
        <Button variant="secondary" onClick={() => onPage(page - 1)}>
          {t('previousPage')}
        </Button>
      )}
      <span>{t('pageOf', { page, pages })}</span>
      {page < pages && (
        <Button variant="secondary" onClick={() => onPage(page + 1)}>
          {t('nextPage')}
        </Button>
      )}
    </nav>
  );
}
