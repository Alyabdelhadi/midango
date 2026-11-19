import { useAuth } from './auth';
import { useI18n } from './i18n';

/** "Good morning, Ali": by the viewer's local hour, with the first word of their display name. */
export function useGreeting() {
  const { staff } = useAuth();
  const { t } = useI18n();
  const hour = new Date().getHours();
  const key = hour < 12 ? 'greetMorning' : hour < 17 ? 'greetAfternoon' : 'greetEvening';
  return t(key, { name: staff?.displayName.trim().split(/\s+/)[0] ?? '' });
}

/** Today's date in the viewer's calendar, as YYYY-MM-DD (for formatDay). */
export function localToday() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
