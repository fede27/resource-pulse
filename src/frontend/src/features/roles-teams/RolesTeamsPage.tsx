import { useState } from 'react';
import { Segmented, Skeleton } from 'antd';
import { useTranslation } from 'react-i18next';
import { PageHeader } from '@/components/domain/PageHeader';
import { SignalCards, type SignalItem } from '@/components/domain/SignalCards';
import { RegistryView } from './RegistryView';
import { AvailabilityTimeline } from './AvailabilityTimeline';
import { useRegistryData } from './useRegistryData';
import { emptyCategoryCount } from './rolesTeamsModel';
import { useStyles } from './RolesTeamsPage.styles';

type View = 'registry' | 'availability';

export function RolesTeamsPage() {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [view, setView] = useState<View>('registry');
  const data = useRegistryData();

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
            : t('rolesTeams.subtitleRegistry')
        }
        signals={
          view === 'registry' && !data.isLoading ? <SignalCards items={signals} /> : undefined
        }
      />

      <Segmented<View>
        className={styles.viewSwitch}
        value={view}
        onChange={setView}
        options={[
          { label: t('rolesTeams.viewRegistry'), value: 'registry' },
          { label: t('rolesTeams.viewAvailability'), value: 'availability' },
        ]}
      />

      {view === 'registry' ? (
        data.isLoading ? (
          <Skeleton active />
        ) : (
          <RegistryView data={data} />
        )
      ) : (
        <AvailabilityTimeline />
      )}
    </div>
  );
}
