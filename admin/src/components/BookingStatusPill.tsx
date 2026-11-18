import { useI18n } from '../lib/i18n';
import type { StaffBooking } from '../lib/types';
import { Pill } from './Pill';

export function BookingStatusPill({
  booking,
}: {
  booking: Pick<StaffBooking, 'status' | 'checkedInAt' | 'cancelledByVenue' | 'lateCancellation'>;
}) {
  const { t } = useI18n();
  if (booking.status === 'NoShow') return <Pill tone="warning">{t('statusNoShow')}</Pill>;
  if (booking.checkedInAt) return <Pill tone="success">{t('statusCheckedIn')}</Pill>;
  if (booking.status === 'Confirmed') return <Pill>{t('statusConfirmed')}</Pill>;
  if (booking.cancelledByVenue) return <Pill>{t('statusCancelledByVenue')}</Pill>;
  if (booking.lateCancellation) return <Pill tone="warning">{t('statusLateCancel')}</Pill>;
  return <Pill>{t('statusCancelled')}</Pill>;
}
