import { useMemo, useState } from 'react';
import { theme } from 'antd';
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

type TabKey = 'calendari' | 'chiusure';

export function TimeConfigPage() {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const [tab, setTab] = useState<TabKey>('calendari');

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
    { key: 'calendari', label: t('timeConfig.tabs.calendars'), count: calCount },
    { key: 'chiusure', label: t('timeConfig.tabs.closures'), count: cloCount },
  ];

  return (
    <div>
      <StickyTabStrip<TabKey>
        items={items}
        activeKey={tab}
        onChange={setTab}
        extra={
          <>
            <ClockCircleOutlined style={{ fontSize: 12 }} />
            <span>{t('common.referenceDate')}:</span>
            <span
              style={{
                fontVariantNumeric: 'tabular-nums',
                color: token.colorTextSecondary,
              }}
            >
              {dayjs().format('D MMMM YYYY')}
            </span>
          </>
        }
      />
      <div style={{ padding: 24, maxWidth: 1440 }}>
        {tab === 'calendari' ? <CalendarsTab /> : <ClosuresTab />}
      </div>
    </div>
  );
}
