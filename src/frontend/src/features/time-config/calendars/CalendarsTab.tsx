import { useMemo, useState } from 'react';
import { App, Card, Col, Empty, Row, Skeleton } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getBusinessCalendarsGetAllQueryKey,
  useBusinessCalendarsCreate,
  useBusinessCalendarsGetAll,
} from '@/api/generated/business-calendars/business-calendars';
import type { BusinessCalendarReadDto } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { PageHeader } from '@/components/domain/PageHeader';
import { CalendarDetail } from './CalendarDetail';
import { CalendarList } from './CalendarList';

export function CalendarsTab() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { message } = App.useApp();
  const showApiError = useApiError();

  const { data, isLoading } = useBusinessCalendarsGetAll();
  const calendars = useMemo(
    () => (data?.data ?? []) as BusinessCalendarReadDto[],
    [data],
  );

  const [pickedId, setPickedId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  // Derived selection: keep the picked calendar if it still exists, else fall
  // back to the first. Avoids syncing selection in an effect.
  const selectedId =
    pickedId && calendars.some((c) => c.id === pickedId)
      ? pickedId
      : (calendars[0]?.id ?? null);

  const createMutation = useBusinessCalendarsCreate({
    mutation: {
      onSuccess: () => {
        message.success(t('timeConfig.calendars.createSuccess'));
        void queryClient.invalidateQueries({ queryKey: getBusinessCalendarsGetAllQueryKey() });
        setCreating(false);
      },
      onError: (e) => showApiError(e),
    },
  });

  const hasDefault = calendars.some((c) => c.isDefault);
  const selected = calendars.find((c) => c.id === selectedId) ?? null;

  if (isLoading) {
    return <Skeleton active />;
  }

  return (
    <>
      <PageHeader
        title={t('timeConfig.calendars.sectionTitle')}
        subtitle={t('timeConfig.calendars.sectionSubtitle')}
      />
      <Row gutter={16} align="top">
      <Col xs={24} md={9} lg={8} xl={7}>
        <CalendarList
          calendars={calendars}
          selectedId={selectedId}
          hasDefault={hasDefault}
          creating={creating}
          submitting={createMutation.isPending}
          onSelect={setPickedId}
          onStartCreate={() => setCreating(true)}
          onCancelCreate={() => setCreating(false)}
          onCreate={({ name, isDefault }) =>
            createMutation.mutate({ data: { name, isDefault } })
          }
        />
      </Col>
      <Col xs={24} md={15} lg={16} xl={17}>
        {selected ? (
          <CalendarDetail
            key={selected.id ?? ''}
            calendar={selected}
            onDeleted={() => setPickedId(null)}
          />
        ) : (
          <Card>
            <Empty description={t('timeConfig.calendars.noneSelectedDescription')} />
          </Card>
        )}
      </Col>
    </Row>
    </>
  );
}
