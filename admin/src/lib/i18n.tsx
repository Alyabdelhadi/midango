import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { ar } from '../locales/ar';
import { en, type MessageKey } from '../locales/en';

export type Lang = 'en' | 'ar';

const dictionaries: Record<Lang, Record<MessageKey, string>> = { en, ar };

type I18n = {
  lang: Lang;
  setLang: (lang: Lang) => void;
  t: (key: MessageKey, vars?: Record<string, string | number>) => string;
  /** The name in the interface language, from any { nameEn, nameAr } record. */
  name: (item: { nameEn: string; nameAr: string }) => string;
};

const I18nContext = createContext<I18n | null>(null);

function storedLang(): Lang {
  try {
    return localStorage.getItem('mg-lang') === 'ar' ? 'ar' : 'en';
  } catch {
    return 'en';
  }
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(storedLang);

  useEffect(() => {
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
  }, [lang]);

  const setLang = useCallback((next: Lang) => {
    setLangState(next);
    try {
      localStorage.setItem('mg-lang', next);
    } catch {
      // Storage blocked (private mode): the choice lasts for this visit only.
    }
  }, []);

  const t = useCallback(
    (key: MessageKey, vars?: Record<string, string | number>) => {
      let text = dictionaries[lang][key];
      for (const [k, v] of Object.entries(vars ?? {})) text = text.replaceAll(`{${k}}`, String(v));
      return text;
    },
    [lang],
  );

  const name = useCallback(
    (item: { nameEn: string; nameAr: string }) => (lang === 'ar' ? item.nameAr : item.nameEn),
    [lang],
  );

  return <I18nContext.Provider value={{ lang, setLang, t, name }}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18n {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error('useI18n must be used inside I18nProvider');
  return ctx;
}
