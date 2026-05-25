import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';
import dayjs from 'dayjs';
import 'dayjs/locale/it';
import 'dayjs/locale/en-gb';
import itIT from 'antd/locale/it_IT';
import enUS from 'antd/locale/en_US';
import type { Locale as AntdLocale } from 'antd/es/locale';

import { it } from './locales/it';
import { en } from './locales/en';

export const SUPPORTED_LANGUAGES = ['it', 'en'] as const;
export type AppLanguage = (typeof SUPPORTED_LANGUAGES)[number];

const DEFAULT_NS = 'app';

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      it: { [DEFAULT_NS]: it },
      en: { [DEFAULT_NS]: en },
    },
    fallbackLng: 'it',
    supportedLngs: [...SUPPORTED_LANGUAGES],
    defaultNS: DEFAULT_NS,
    ns: [DEFAULT_NS],
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'rp-lang',
      caches: ['localStorage'],
    },
    returnNull: false,
  });

const ANTD_LOCALES: Record<AppLanguage, AntdLocale> = {
  it: itIT,
  en: enUS,
};

const DAYJS_LOCALES: Record<AppLanguage, string> = {
  it: 'it',
  en: 'en-gb',
};

export function antdLocaleFor(lng: string): AntdLocale {
  return ANTD_LOCALES[normalizeLanguage(lng)];
}

export function syncDayjsLocale(lng: string): void {
  dayjs.locale(DAYJS_LOCALES[normalizeLanguage(lng)]);
}

export function normalizeLanguage(lng: string): AppLanguage {
  const base = (lng ?? '').toLowerCase().split('-')[0] ?? '';
  return (SUPPORTED_LANGUAGES as readonly string[]).includes(base)
    ? (base as AppLanguage)
    : 'it';
}

// Sync dayjs to whatever i18next resolved at init time.
syncDayjsLocale(i18n.resolvedLanguage ?? i18n.language ?? 'it');

i18n.on('languageChanged', (lng) => {
  syncDayjsLocale(lng);
});

export default i18n;
