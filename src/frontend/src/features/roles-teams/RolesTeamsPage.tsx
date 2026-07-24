import { useState } from 'react';
import { Segmented, Skeleton } from 'antd';
import { CheckCircleFilled, WarningFilled } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { PageHeader } from '@/components/domain/PageHeader';
import { AnagraficaView } from './AnagraficaView';
import { AvailabilityTimeline } from './AvailabilityTimeline';
import { useAnagraficaData } from './useAnagraficaData';
import { emptyCategoryCount } from './rolesTeamsModel';
import { useStyles } from './RolesTeamsPage.styles';

type View = 'anagrafica' | 'availability';

export function RolesTeamsPage() {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const [view, setView] = useState<View>('anagrafica');
  const data = useAnagraficaData();

  const emptyRoles = emptyCategoryCount(data.roles, data.people, 'role');
  const emptyTeams = emptyCategoryCount(data.teams, data.people, 'team');

  const headerActions =
    view === 'anagrafica' && !data.isLoading ? (
      <div className={styles.chipRow}>
        {emptyRoles === 0 && emptyTeams === 0 ? (
          <span className={cx(styles.chip, styles.chipOk)}>
            <CheckCircleFilled /> {t('rolesTeams.allRolesCovered')}
          </span>
        ) : (
          <>
            {emptyRoles > 0 && (
              <span className={cx(styles.chip, styles.chipWarn)}>
                <WarningFilled />
                <span className={styles.chipCount}>{emptyRoles}</span>{' '}
                {emptyRoles === 1
                  ? t('rolesTeams.chip.emptyRolesOne')
                  : t('rolesTeams.chip.emptyRoles')}
              </span>
            )}
            {emptyTeams > 0 && (
              <span className={cx(styles.chip, styles.chipWarn)}>
                <WarningFilled />
                <span className={styles.chipCount}>{emptyTeams}</span>{' '}
                {emptyTeams === 1
                  ? t('rolesTeams.chip.emptyTeamsOne')
                  : t('rolesTeams.chip.emptyTeams')}
              </span>
            )}
          </>
        )}
      </div>
    ) : undefined;

  return (
    <div>
      <PageHeader
        title={t('rolesTeams.title')}
        subtitle={
          view === 'availability'
            ? t('rolesTeams.subtitleAvailability')
            : t('rolesTeams.subtitleAnagrafica')
        }
        actions={headerActions}
      />

      <Segmented<View>
        className={styles.viewSwitch}
        value={view}
        onChange={setView}
        options={[
          { label: t('rolesTeams.viewAnagrafica'), value: 'anagrafica' },
          { label: t('rolesTeams.viewAvailability'), value: 'availability' },
        ]}
      />

      {view === 'anagrafica' ? (
        data.isLoading ? (
          <Skeleton active />
        ) : (
          <AnagraficaView data={data} />
        )
      ) : (
        <AvailabilityTimeline />
      )}
    </div>
  );
}
