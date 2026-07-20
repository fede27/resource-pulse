import { LockOutlined, SwapOutlined, WarningOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { alpha, blue, neutral } from '@/app/palette';
import type { Verdict } from './boardModel';
import {
  BLOCK_ACCENT,
  BLOCK_BORDER,
  BLOCK_HARD_BG,
  FENCE_FROZEN_TEXT,
  HOLE_ACCENT,
  HOLE_BG,
  MISMATCH_TEXT,
  OVER_ALLOC_TEXT,
  VERDICT_COLORS,
} from './boardColors';
import { useStyles } from './BoardLegend.styles';

const VERDICTS: Verdict[] = ['sustainable', 'atRisk', 'uncovered'];

// Two legend rows: the HERO axis (sustainability verdict) and the context axes
// (block status, open role, conflict/mismatch flags, provenance, fence zones).
export function BoardLegend({ overloadThreshold }: { overloadThreshold: number }) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();

  return (
    <div className={styles.legend}>
      <div className={styles.row}>
        <span className={styles.title}>{t('projects.legend.title')}</span>
        {VERDICTS.map((v) => {
          const c = VERDICT_COLORS[v];
          return (
            <span key={v} className={styles.item} title={t(`projects.legend.rule.${v}`)}>
              {/* dynamic: verdict stripe colour from semantic palette. */}
              <span className={styles.stripe} style={{ background: c.stripe }} />
              {/* dynamic: verdict text colour from semantic palette. */}
              <span style={{ fontWeight: 600, color: c.color }}>{t(`projects.verdict.${v}`)}</span>
            </span>
          );
        })}
        <span className={styles.hint}>{t('projects.legend.hint', { threshold: overloadThreshold })}</span>
      </div>
      <div className={cx(styles.row, styles.rowSecondary)}>
        <span className={styles.item}>
          {/* dynamic: swatch reproduces the hard-block bar look. */}
          <span
            className={styles.swatch}
            style={{ background: BLOCK_HARD_BG, border: `1px solid ${BLOCK_BORDER}`, borderLeft: `3px solid ${BLOCK_ACCENT}` }}
          />
          {t('projects.legend.hard')}
        </span>
        <span className={styles.item}>
          {/* dynamic: swatch reproduces the tentative-block hatch. */}
          <span
            className={styles.swatch}
            style={{
              background: `repeating-linear-gradient(135deg, ${alpha(blue[5], 0.1)} 0 5px, ${alpha(blue[5], 0.02)} 5px 10px)`,
              border: `1px dashed ${BLOCK_BORDER}`,
            }}
          />
          {t('projects.legend.tentative')}
        </span>
        <span className={styles.item}>
          {/* dynamic: swatch reproduces the uncovered-demand hatch. */}
          <span
            className={styles.swatch}
            style={{
              background: `repeating-linear-gradient(135deg, ${HOLE_BG} 0 5px, ${neutral.white} 5px 10px)`,
              border: `1.5px dashed ${HOLE_ACCENT}`,
            }}
          />
          {t('projects.legend.hole')}
        </span>
        {/* dynamic: flag colours from semantic palette. */}
        <span className={styles.item} style={{ color: OVER_ALLOC_TEXT }}>
          <WarningOutlined /> {t('projects.legend.conflict')}
        </span>
        <span className={styles.item} style={{ color: MISMATCH_TEXT }}>
          <SwapOutlined /> {t('projects.legend.mismatch')}
        </span>
        <span className={styles.divider} />
        <span>
          {t('projects.legend.provenance')} <span className={styles.chipSolid}>{t('projects.provenance.committed')}</span>{' '}
          <span className={styles.chipDashed}>{t('projects.provenance.proposed')}</span>
        </span>
        <span className={styles.divider} />
        {/* dynamic: fence zone colour from semantic palette. */}
        <span className={styles.item} style={{ color: FENCE_FROZEN_TEXT }}>
          <LockOutlined /> {t('projects.legend.frozen')}
        </span>
        <span>{t('projects.legend.slushy')}</span>
        <span className={styles.hint}>{t('projects.legend.liquid')}</span>
      </div>
    </div>
  );
}
