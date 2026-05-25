import 'i18next';
import type { it } from './locales/it';

// Type-augment i18next so `t('common.save')` is autocompleted and unknown
// keys are compile errors. `it` is the source of truth — the English file is
// declared with `typeof it` so the shapes can never drift.
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'app';
    resources: {
      app: typeof it;
    };
  }
}
