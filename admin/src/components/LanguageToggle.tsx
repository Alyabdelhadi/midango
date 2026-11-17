import { useI18n } from '../lib/i18n';
import { Button } from './Button';

export function LanguageToggle() {
  const { lang, setLang } = useI18n();
  const other = lang === 'en' ? 'ar' : 'en';
  return (
    <Button variant="text" lang={other} onClick={() => setLang(other)}>
      {other === 'ar' ? 'العربية' : 'English'}
    </Button>
  );
}
