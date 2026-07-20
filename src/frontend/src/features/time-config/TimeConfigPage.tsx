import { useMemo, useState } from 'react';
import { ClockCircleOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useBusinessCalendarsGetAll } from '@/api/generated/business-calendars/business-calendars';
import { useCompanyClosuresGetAll } from '@/api/generated/company-closures/company-closures';
import type {
  BusinessCalendarReadDto,
  CompanyClosureReadDto,
} from '@/api/generated/schemas';
import { StickyTabStrip, type StickyTabItem } from '@/components/domain/StickyTabStrip';
import { CalendarsTab } from './calendars/CalendarsTab';
import { ClosuresTab } from './closures/ClosuresTab';
import { useStyles } from './TimeConfigPage.styles';

type TabKey = 'calendars' | 'closures';

export function TimeConfigPage() {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [tab, setTab] = useState<TabKey>('calendars');

  const { data: calData } = useBusinessCalendarsGetAll();
  const { data: cloData } = useCompanyClosuresGetAll();

  const calCount = useMemo(
    () => ((calData?.data ?? []) as BusinessCalendarReadDto[]).length,
    [calData],
  );
  const cloCount = useMemo(
    () => ((cloData?.data ?? []) as CompanyClosureReadDto[]).length,
    [cloData],
  );

  const items: StickyTabItem<TabKey>[] = [
    { key: 'calendars', label: t('timeConfig.tabs.calendars'), count: calCount },
    { key: 'closures', label: t('timeConfig.tabs.closures'), count: cloCount },
  ];

  return (
    <div>
      <StickyTabStrip<TabKey>
        items={items}
        activeKey={tab}
        onChange={setTab}
        extra={
          <>
            <ClockCircleOutlined className={styles.refIcon} />
            <span>{t('common.referenceDate')}:</span>
            <span className={styles.refDate}>{dayjs().format('D MMMM YYYY')}</span>
          </>
        }
      />
      <div className={styles.content}>
        {tab === 'calendars' ? <CalendarsTab /> : <ClosuresTab />}
      </div>
    </div>
  );
}
