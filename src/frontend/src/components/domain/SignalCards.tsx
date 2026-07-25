import type { ReactNode } from 'react';
import { useStyles } from './SignalCards.styles';

// Every page states its global health the same way: one strip of at most three
// compact cards in the header's right slot. Not a dashboard — a signal.
export type SignalTone = 'ok' | 'warning' | 'danger' | 'neutral';

export type SignalItem = {
  key: string;
  /** Short noun phrase — pre-resolved copy, not an i18n key. */
  label: ReactNode;
  /** The number that carries the signal (or "8 / 12"). */
  value: ReactNode;
  /** One short qualifier: a threshold, a breakdown. Optional by design. */
  hint?: ReactNode;
  tone?: SignalTone;
};

// The cap is enforced here, not at the call sites: a fourth card would silently
// re-open the "header as dashboard" drift this component exists to close.
export const MAX_SIGNAL_CARDS = 3;

const TONE_CARD: Record<SignalTone, keyof ReturnType<typeof useStyles>['styles']> = {
  ok: 'toneOk',
  warning: 'toneWarning',
  danger: 'toneDanger',
  neutral: 'toneNeutral',
};

const TONE_VALUE: Record<SignalTone, keyof ReturnType<typeof useStyles>['styles']> = {
  ok: 'toneOkValue',
  warning: 'toneWarningValue',
  danger: 'toneDangerValue',
  neutral: 'toneNeutralValue',
};

export function SignalCards({ items }: { items: readonly SignalItem[] }) {
  const { styles, cx } = useStyles();
  const shown = items.slice(0, MAX_SIGNAL_CARDS);
  if (shown.length === 0) return null;

  return (
    <div className={styles.row}>
      {shown.map((it) => {
        const tone = it.tone ?? 'neutral';
        return (
          <div key={it.key} className={cx(styles.card, styles[TONE_CARD[tone]])}>
            <div className={styles.head}>
              <span className={cx(styles.value, styles[TONE_VALUE[tone]])}>{it.value}</span>
              <span className={styles.label}>{it.label}</span>
            </div>
            {it.hint && <div className={styles.hint}>{it.hint}</div>}
          </div>
        );
      })}
    </div>
  );
}
