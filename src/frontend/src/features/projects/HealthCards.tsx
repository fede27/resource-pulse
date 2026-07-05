import { theme } from 'antd';
import { useTranslation } from 'react-i18next';
import type { PortfolioHealth } from './boardModel';
import { HOLE_ACCENT } from './boardColors';
import { useStyles } from './HealthCards.styles';

// The three portfolio health cards — signal, not nude counts. Derived
// client-side over ALL projects (project-gap.md §★★).
export function HealthCards({
  health,
  overloadThreshold,
}: {
  health: PortfolioHealth;
  overloadThreshold: number;
}) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { token } = theme.useToken();

  const ok = token.colorSuccess;
  const sustainableAccent = health.sustainable === health.total ? ok : '#d46b08';
  const holesAccent = health.totalHoles ? HOLE_ACCENT : ok;
  const overloadedAccent = health.overloadedPeople ? token.colorError : ok;

  const cards = [
    {
      key: 'sustainable',
      label: t('projects.health.sustainable'),
      value: `${health.sustainable} / ${health.total}`,
      accent: sustainableAccent,
      foot:
        health.atRisk > 0 || health.uncovered > 0
          ? t('projects.health.sustainableFootIssues', {
              atRisk: health.atRisk,
              uncovered: health.uncovered,
            })
          : t('projects.health.sustainableFootOk'),
    },
    {
      key: 'holes',
      label: t('projects.health.holes'),
      value: String(health.totalHoles),
      accent: holesAccent,
      foot: health.totalHoles ? t('projects.health.holesFoot') : t('projects.health.holesFootOk'),
    },
    {
      key: 'overloaded',
      label: t('projects.health.overloaded'),
      value: String(health.overloadedPeople),
      accent: overloadedAccent,
      foot: t('projects.health.overloadedFoot', { threshold: overloadThreshold }),
    },
  ];

  return (
    <div className={styles.row}>
      {cards.map((c) => (
        // dynamic: accent colour is resolved from live health data.
        <div key={c.key} className={styles.card} style={{ borderLeftColor: c.accent }}>
          <div className={styles.label}>{c.label}</div>
          {/* dynamic: value colour mirrors the accent. */}
          <div className={styles.value} style={{ color: c.accent }}>
            {c.value}
          </div>
          <div className={styles.foot}>{c.foot}</div>
        </div>
      ))}
    </div>
  );
}
