import { useState } from 'react';
import { Segmented, Skeleton } from 'antd';
import { useTranslation } from 'react-i18next';
import { PageHeader } from '@/components/domain/PageHeader';
import { SignalCards, type SignalItem } from '@/components/domain/SignalCards';
import { AnagraficaView } from './AnagraficaView';
import { AvailabilityTimeline } from './AvailabilityTimeline';
import { useAnagraficaData } from './useAnagraficaData';
import { emptyCategoryCount } from './rolesTeamsModel';
import { useStyles } from './RolesTeamsPage.styles';

type View = 'anagrafica' | 'availability';

export function RolesTeamsPage() {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [view, setView] = useState<View>('anagrafica');
  const data = useAnagraficaData();

  const emptyRoles = emptyCategoryCount(data.roles, data.people, 'role');
  const emptyTeams = emptyCategoryCount(data.teams, data.people, 'team');

  // Always the same two cards, tone-switched — a stable strip beats a chip that
  // appears and disappears under the title.
  const signals: SignalItem[] = [
    {
      key: 'emptyRoles',
      label: t('rolesTeams.signal.emptyRoles'),
      value: emptyRoles,
      tone: emptyRoles ? 'warning' : 'ok',
    },
    {
      key: 'emptyTeams',
      label: t('rolesTeams.signal.emptyTeams'),
      value: emptyTeams,
      tone: emptyTeams ? 'warning' : 'ok',
    },
  ];

  return (
    <div>
      <PageHeader
        title={t('rolesTeams.title')}
        subtitle={
          view === 'availability'
            ? t('rolesTeams.subtitleAvailability')
            : t('rolesTeams.subtitleAnagrafica')
        }
        signals={
          view === 'anagrafica' && !data.isLoading ? <SignalCards items={signals} /> : undefined
        }
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
