import { useState } from 'react';
import { Button } from '../components/Button';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { SportIcon } from '../lib/sportIcons';
import type { Category } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import type { MessageKey } from '../locales/en';
import { ListToolbar, matches, useInitialQuery } from '../components/ListToolbar';
import { CategoryDialog } from './CategoryDialog';

type CategoryFilter = 'all' | 'shown' | 'hidden';
const CATEGORY_FILTERS: [CategoryFilter, MessageKey][] = [
  ['all', 'filterAll'],
  ['shown', 'shownInApp'],
  ['hidden', 'hidden'],
];

export function CategoriesPage() {
  const { t, name } = useI18n();
  const categories = useLoad(() => api<Category[]>('/admin/categories'));
  const [editing, setEditing] = useState<Category | 'new' | null>(null);
  const list = categories.data;
  const [q, setQ] = useState(useInitialQuery());
  const [shownFilter, setShownFilter] = useState<CategoryFilter>('all');
  const shown = list?.filter(
    (c) => matches(`${c.nameEn} ${c.nameAr}`, q) && (shownFilter === 'all' || c.active === (shownFilter === 'shown')),
  );

  return (
    <div className="page">
      <PageHeader title={t('navCategories')} action={<Button onClick={() => setEditing('new')}>{t('newCategory')}</Button>} />
      <p className="page-intro">{t('categoriesIntro')}</p>
      {categories.error && <Notice tone="error">{categories.error}</Notice>}
      {list?.length === 0 && <p className="empty">{t('noCategories')}</p>}
      {list && list.length > 0 && (
        <ListToolbar
          q={q}
          onQ={setQ}
          placeholder={t('searchSports')}
          filters={CATEGORY_FILTERS}
          filter={shownFilter}
          onFilter={setShownFilter}
          shown={shown!.length}
          total={list.length}
        />
      )}
      {shown?.length === 0 && list!.length > 0 && <p className="empty">{t('noMatches')}</p>}
      {shown && shown.length > 0 && (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('sport')}</th>
              <th>{t('bookingLengths')}</th>
              <th>{t('playersPerMatch')}</th>
              <th>{t('courtFeatures')}</th>
              <th>{t('status')}</th>
              <th>
                <span className="visually-hidden">{t('actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {shown.map((category) => (
              <tr key={category.id}>
                <td>
                  <span className="cell-icon">
                    <SportIcon iconKey={category.iconKey} iconUrl={category.iconUrl} size={18} />
                    {name(category)}
                  </span>
                </td>
                <td className="numeric">{category.durationsMinutes.map((m) => t('minutesShort', { m })).join(' · ')}</td>
                <td className="numeric">{category.playersPerMatch}</td>
                <td>{category.features.map((f) => name(f)).join(', ') || '—'}</td>
                <td>
                  <Pill tone={category.active ? 'success' : 'neutral'}>{category.active ? t('shownInApp') : t('hidden')}</Pill>
                </td>
                <td className="row-actions">
                  <Button variant="text" onClick={() => setEditing(category)}>
                    {t('edit')}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <CategoryDialog
        category={editing}
        nextSortOrder={(list?.length ?? 0) + 1}
        onClose={() => setEditing(null)}
        onIconChanged={categories.reload}
        onSaved={() => {
          setEditing(null);
          categories.reload();
        }}
      />
    </div>
  );
}
