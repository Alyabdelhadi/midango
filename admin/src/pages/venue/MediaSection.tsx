import { ChevronLeft, ChevronRight, ImageOff } from 'lucide-react';
import { useState } from 'react';
import { Button } from '../../components/Button';
import { UploadButton } from '../../components/UploadButton';
import { api, fileBody, mediaUrl } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import type { Photo, Venue } from '../../lib/types';

const MAX_PHOTOS = 10;

/** `change` runs one API call, merges what it returns into the venue and reports errors; false = it failed. */
export type MediaChange = (action: () => Promise<Partial<Venue>>) => Promise<boolean>;

export function MediaSection({ venue, canEdit, change }: { venue: Venue; canEdit: boolean; change: MediaChange }) {
  const { t } = useI18n();
  const [busy, setBusy] = useState(false);
  const photos = venue.photos;

  async function guarded(work: () => Promise<unknown>) {
    setBusy(true);
    try {
      await work();
    } finally {
      setBusy(false);
    }
  }

  const uploadLogo = (file: File) =>
    guarded(() =>
      change(async () => {
        const { logoUrl } = await api<{ logoUrl: string }>('/venue/logo', { method: 'PUT', body: fileBody(file) });
        return { logoUrl };
      }),
    );

  const removeLogo = () =>
    guarded(() =>
      change(async () => {
        await api('/venue/logo', { method: 'DELETE' });
        return { logoUrl: null };
      }),
    );

  // One request per file, in the order picked; stop at the first refusal (too big, wrong format, full).
  const addPhotos = (files: File[]) =>
    guarded(async () => {
      for (const file of files) {
        const ok = await change(async () => ({
          photos: await api<Photo[]>('/venue/photos', { method: 'POST', body: fileBody(file) }),
        }));
        if (!ok) break;
      }
    });

  const removePhoto = (id: string) =>
    guarded(() => change(async () => ({ photos: await api<Photo[]>(`/venue/photos/${id}`, { method: 'DELETE' }) })));

  const movePhoto = (index: number, by: -1 | 1) => {
    const ids = photos.map((p) => p.id);
    [ids[index], ids[index + by]] = [ids[index + by], ids[index]];
    return guarded(() =>
      change(async () => ({ photos: await api<Photo[]>('/venue/photos/order', { method: 'PUT', body: ids }) })),
    );
  };

  return (
    <section className="card">
      <h2>{t('logoAndPhotos')}</h2>
      {canEdit && <p className="mg-field-hint">{t('mediaHint')}</p>}

      <div className="media-logo">
        {venue.logoUrl ? (
          <img src={mediaUrl(venue.logoUrl)} alt={t('venueLogo')} width={96} height={96} className="media-logo-img" />
        ) : (
          <span className="media-logo-img media-placeholder">
            <ImageOff size={24} aria-hidden="true" />
            <span className="visually-hidden">{t('noLogo')}</span>
          </span>
        )}
        {canEdit && (
          <div className="chip-row">
            <UploadButton disabled={busy} onFiles={([file]) => void uploadLogo(file)}>
              {t('uploadLogo')}
            </UploadButton>
            {venue.logoUrl && (
              <Button variant="text" disabled={busy} onClick={() => void removeLogo()}>
                {t('removeLogo')}
              </Button>
            )}
          </div>
        )}
      </div>

      <div className="card-head">
        <h3 className="media-subhead">{t('photos')}</h3>
        <span className="mg-field-hint">{t('photoCount', { n: photos.length, max: MAX_PHOTOS })}</span>
      </div>
      {photos.length === 0 && <p className="empty">{t('noPhotos')}</p>}
      {photos.length > 0 && (
        <ol className="media-grid">
          {photos.map((photo, index) => (
            <li key={photo.id} className="media-tile">
              <img src={mediaUrl(photo.url)} alt={t('photoNumber', { n: index + 1 })} />
              {canEdit && (
                <div className="media-tile-actions">
                  <Button
                    variant="secondary"
                    aria-label={t('moveEarlier')}
                    disabled={busy || index === 0}
                    onClick={() => void movePhoto(index, -1)}
                  >
                    <ChevronLeft className="flip-rtl" size={18} aria-hidden="true" />
                  </Button>
                  <Button
                    variant="secondary"
                    aria-label={t('moveLater')}
                    disabled={busy || index === photos.length - 1}
                    onClick={() => void movePhoto(index, 1)}
                  >
                    <ChevronRight className="flip-rtl" size={18} aria-hidden="true" />
                  </Button>
                  <Button variant="text" disabled={busy} onClick={() => void removePhoto(photo.id)}>
                    {t('remove')}
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ol>
      )}
      {canEdit && (
        <div>
          <UploadButton
            multiple
            disabled={busy}
            disabledReason={photos.length >= MAX_PHOTOS ? t('photosFull') : undefined}
            onFiles={(files) => void addPhotos(files)}
          >
            {t('addPhotos')}
          </UploadButton>
        </div>
      )}
    </section>
  );
}
