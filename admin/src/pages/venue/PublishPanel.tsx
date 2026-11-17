import { Button } from '../../components/Button';
import { Pill } from '../../components/Pill';
import { useI18n } from '../../lib/i18n';
import { CHECKLIST, MISSING_KEY, type Venue } from '../../lib/types';

export function PublishPanel({
  venue,
  canEdit,
  onPublish,
  onUnpublish,
}: {
  venue: Venue;
  canEdit: boolean;
  onPublish: () => void;
  onUnpublish: () => void;
}) {
  const { t } = useI18n();
  return (
    <section className="card">
      <div className="card-head">
        <h2>{t('appVisibility')}</h2>
        <Pill tone={venue.published ? 'success' : 'neutral'}>{venue.published ? t('liveInApp') : t('notPublished')}</Pill>
      </div>
      {!venue.published && (
        <ul className="checklist">
          {CHECKLIST.map((item) => {
            const done = !venue.missingForPublish.includes(item);
            return (
              <li key={item} className={done ? 'done' : 'todo'}>
                {done ? '✓' : '○'} {t(MISSING_KEY[item])}
              </li>
            );
          })}
        </ul>
      )}
      {canEdit && (
        <div>
          {venue.published ? (
            <Button variant="secondary" onClick={onUnpublish}>
              {t('unpublish')}
            </Button>
          ) : (
            <Button
              onClick={onPublish}
              disabledReason={venue.missingForPublish.length > 0 ? t('finishChecklist') : undefined}
            >
              {t('publish')}
            </Button>
          )}
        </div>
      )}
    </section>
  );
}
