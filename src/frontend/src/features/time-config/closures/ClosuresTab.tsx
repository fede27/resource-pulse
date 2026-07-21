import { useMemo, useState } from 'react';
import { App, Button, Card, Col, Empty, Flex, Row, Skeleton } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import {
  getCompanyClosuresGetAllQueryKey,
  useCompanyClosuresCreate,
  useCompanyClosuresDelete,
  useCompanyClosuresGetAll,
  useCompanyClosuresUpdate,
} from '@/api/generated/company-closures/company-closures';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { PageHeader } from '@/components/domain/PageHeader';
import { StatCard } from '@/components/domain/StatCard';
import { YearSelector } from '@/components/domain/YearSelector';
import { ClosureInlineForm, type ClosureFormValues } from './ClosureInlineForm';
import {
  ClosureInspectPopover,
  type ClosureInspectAnchor,
} from './ClosureInspectPopover';
import { ClosureSection } from './ClosureSection';
import { useStyles } from './ClosuresTab.styles';

type EditState =
  | { kind: 'idle' }
  | { kind: 'creating' }
  | { kind: 'editing'; id: string };

export function ClosuresTab() {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { message, modal } = App.useApp();
  const queryClient = useQueryClient();
  const showApiError = useApiError();

  const { data, isLoading } = useCompanyClosuresGetAll();
  const allClosures = useMemo(
    () => (data?.data ?? []) as CompanyClosureReadDto[],
    [data],
  );

  const [year, setYear] = useState<number>(() => dayjs().year());
  const [editState, setEditState] = useState<EditState>({ kind: 'idle' });
  const [inspect, setInspect] = useState<{
    closure: CompanyClosureReadDto;
    anchor: ClosureInspectAnchor;
  } | null>(null);

  const openInspect = (closure: CompanyClosureReadDto, anchor: ClosureInspectAnchor) =>
    setInspect({ closure, anchor });
  const closeInspect = () => setInspect(null);
  const startEdit = (c: CompanyClosureReadDto) => {
    if (c.id) setEditState({ kind: 'editing', id: c.id });
  };

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: getCompanyClosuresGetAllQueryKey() });

  const createMutation = useCompanyClosuresCreate({
    mutation: {
      onSuccess: (_, variables) => {
        const reason = variables.data?.reason ?? '';
        message.success(
          reason
            ? t('timeConfig.closures.addSuccess', { reason })
            : t('timeConfig.closures.addSuccessFallback'),
        );
        void invalidate();
        setEditState({ kind: 'idle' });
      },
      onError: (e) => showApiError(e),
    },
  });

  const updateMutation = useCompanyClosuresUpdate({
    mutation: {
      onSuccess: () => {
        message.success(t('timeConfig.closures.updateSuccess'));
        void invalidate();
        setEditState({ kind: 'idle' });
      },
      onError: (e) => showApiError(e),
    },
  });

  const deleteMutation = useCompanyClosuresDelete({
    mutation: {
      onSuccess: () => {
        message.success(t('timeConfig.closures.deleteSuccess'));
        void invalidate();
      },
      onError: (e) => showApiError(e),
    },
  });

  const handleSubmit = (values: ClosureFormValues) => {
    const dto = {
      dateFrom: values.dateFrom.format('YYYY-MM-DD'),
      dateTo: values.dateTo.format('YYYY-MM-DD'),
      reason: values.reason,
    };
    if (editState.kind === 'editing') {
      updateMutation.mutate({ id: editState.id, data: dto });
    } else {
      createMutation.mutate({ data: dto });
    }
  };

  const confirmDelete = (c: CompanyClosureReadDto) => {
    if (!c.id) return;
    modal.confirm({
      title: t('timeConfig.closures.deleteConfirmTitle'),
      content: (
        <>
          <strong>{c.reason ?? '—'}</strong> (
          {c.dateFrom === c.dateTo ? c.dateFrom : `${c.dateFrom} → ${c.dateTo}`}).{' '}
          {t('common.irreversibleAction')}
        </>
      ),
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => deleteMutation.mutateAsync({ id: c.id! }).catch(() => undefined),
    });
  };

  // Year-scoped slice
  const yearClosures = useMemo(() => {
    const yStart = dayjs(`${year}-01-01`);
    const yEnd = dayjs(`${year}-12-31`);
    return allClosures
      .filter((c) => {
        const from = c.dateFrom ? dayjs(c.dateFrom) : null;
        const to = c.dateTo ? dayjs(c.dateTo) : from;
        if (!from || !to) return false;
        return !to.isBefore(yStart, 'day') && !from.isAfter(yEnd, 'day');
      })
      .sort((a, b) => (a.dateFrom ?? '').localeCompare(b.dateFrom ?? ''));
  }, [allClosures, year]);

  const today = dayjs().startOf('day');
  const upcoming = yearClosures.filter((c) => {
    const to = c.dateTo ? dayjs(c.dateTo) : null;
    return !to || !to.isBefore(today, 'day');
  });
  const past = yearClosures.filter((c) => {
    const to = c.dateTo ? dayjs(c.dateTo) : null;
    return !!to && to.isBefore(today, 'day');
  });

  // Plain computation — the React Compiler memoizes it. A manual useMemo here
  // tripped preserve-manual-memoization (its inferred dep was `upcoming`, not
  // `upcoming.length`).
  const stats = (() => {
    const yStart = dayjs(`${year}-01-01`);
    const yEnd = dayjs(`${year}-12-31`);
    const daySet = new Set<string>();
    yearClosures.forEach((c) => {
      const from = c.dateFrom ? dayjs(c.dateFrom) : null;
      const to = c.dateTo ? dayjs(c.dateTo) : from;
      if (!from || !to) return;
      let d = from.isBefore(yStart, 'day') ? yStart : from;
      const cap = to.isAfter(yEnd, 'day') ? yEnd : to;
      while (!d.isAfter(cap, 'day')) {
        daySet.add(d.format('YYYY-MM-DD'));
        d = d.add(1, 'day');
      }
    });
    return {
      total: yearClosures.length,
      days: daySet.size,
      futureCount: upcoming.length,
    };
  })();

  const availableYears = useMemo(() => {
    const years = new Set<number>();
    allClosures.forEach((c) => {
      if (c.dateFrom) years.add(dayjs(c.dateFrom).year());
      if (c.dateTo) years.add(dayjs(c.dateTo).year());
    });
    const current = dayjs().year();
    [current - 1, current, current + 1, current + 2].forEach((y) => years.add(y));
    return [...years].sort((a, b) => a - b);
  }, [allClosures]);

  const editingId = editState.kind === 'editing' ? editState.id : null;
  const creating = editState.kind === 'creating';
  const submitting = createMutation.isPending || updateMutation.isPending;

  const formForEdit = (closure: CompanyClosureReadDto) => (
    <ClosureInlineForm
      initial={closure}
      yearHint={year}
      saving={submitting}
      deleting={deleteMutation.isPending}
      onSubmit={handleSubmit}
      onCancel={() => setEditState({ kind: 'idle' })}
      onDelete={() => confirmDelete(closure)}
    />
  );

  const editingClosure =
    editingId ? yearClosures.find((c) => c.id === editingId) ?? null : null;

  // Place the editing form above the section that contains the row being edited.
  const editingInUpcoming = !!editingClosure && upcoming.some((c) => c.id === editingClosure.id);
  const editingInPast = !!editingClosure && past.some((c) => c.id === editingClosure.id);

  if (isLoading) return <Skeleton active />;

  const currentYear = dayjs().year();
  const isPastYear = year < currentYear;
  const upcomingValue = isPastYear ? 0 : stats.futureCount;

  return (
    <div>
      <PageHeader
        title={t('timeConfig.closures.sectionTitle')}
        subtitle={t('timeConfig.closures.sectionSubtitle')}
        actions={
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setEditState({ kind: 'creating' })}
            disabled={editState.kind !== 'idle'}
          >
            {t('timeConfig.closures.newButton')}
          </Button>
        }
      />
      <div className={styles.mb}>
        <YearSelector value={year} onChange={setYear} availableYears={availableYears} />
      </div>

      <Row gutter={16} className={styles.mb}>
        <Col xs={24} md={8}>
          <StatCard
            label={t('timeConfig.closures.statTitleClosuresInYear', { year })}
            value={stats.total}
            suffix={t('timeConfig.closures.statsTotal', { count: stats.total })}
          />
        </Col>
        <Col xs={24} md={8}>
          <StatCard
            label={t('timeConfig.closures.statTitleClosureDays')}
            value={stats.days}
            suffix={t('timeConfig.closures.statsDays', { count: stats.days })}
          />
        </Col>
        <Col xs={24} md={8}>
          <StatCard
            label={
              isPastYear
                ? t('timeConfig.closures.statTitleAllConcluded')
                : t('timeConfig.closures.statTitleUpcoming')
            }
            value={upcomingValue}
            suffix={t('timeConfig.closures.statsTotal', { count: upcomingValue })}
          />
        </Col>
      </Row>

      <Flex vertical gap={16}>
        <ClosureSection
          title={t('timeConfig.closures.sectionUpcoming')}
          status="upcoming"
          closures={upcoming}
          inlineSlot={
            creating ? (
              <ClosureInlineForm
                yearHint={year}
                saving={submitting}
                onSubmit={handleSubmit}
                onCancel={() => setEditState({ kind: 'idle' })}
              />
            ) : editingInUpcoming && editingClosure ? (
              formForEdit(editingClosure)
            ) : undefined
          }
          hiddenRowId={editingInUpcoming ? editingId : null}
          defaultOpen
          onInspect={openInspect}
          onEdit={startEdit}
          onDelete={confirmDelete}
        />
        <ClosureSection
          title={t('timeConfig.closures.sectionPast')}
          status="past"
          closures={past}
          inlineSlot={editingInPast && editingClosure ? formForEdit(editingClosure) : undefined}
          hiddenRowId={editingInPast ? editingId : null}
          defaultOpen={upcoming.length === 0}
          onInspect={openInspect}
          onEdit={startEdit}
          onDelete={confirmDelete}
        />

        {yearClosures.length === 0 && !creating && (
          <Card>
            <Empty description={t('timeConfig.closures.emptyForYear', { year })} />
          </Card>
        )}
      </Flex>

      {inspect && (
        <ClosureInspectPopover
          closure={inspect.closure}
          anchor={inspect.anchor}
          onClose={closeInspect}
          onEdit={startEdit}
          onDelete={confirmDelete}
        />
      )}
    </div>
  );
}
