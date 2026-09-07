// Dashboard — data layer. Four reads, all domain-shaped (ADR-0028):
//   1 × GET /api/signals?scope        (the queue, already ranked server-side)
//   1 × GET /api/signals/changes      ("cosa è cambiato", since the last visit)
//   1 × GET /api/signals/sweep        (have we looked, and when)
//   1 × GET /api/me                   (identity, for the scope and the gating)
//
// Constant in tenant size, by construction: the queue has a fixed budget and the
// aggregate kinds collapse their occurrences into one row each. No fan-out to
// unwind here — the detector already did the expensive part, offline.

import { useCallback, useEffect, useMemo, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';

import {
  getSignalsGetQueryKey,
  getSignalsGetChangesQueryKey,
  getSignalsGetSweepQueryKey,
  useSignalsAcknowledge,
  useSignalsGet,
  useSignalsGetChanges,
  useSignalsGetSweep,
  useSignalsRecordVisit,
  useSignalsReopen,
} from '@/api/generated/signals/signals';
import { getAllocationsGetInRangeQueryKey } from '@/api/generated/allocations/allocations';
import {
  getLoadGetDemandCoverageInRangeQueryKey,
  getLoadGetResourceLoadProfilesQueryKey,
} from '@/api/generated/load/load';
import { usePlanCommandsExecute } from '@/api/generated/plan-commands/plan-commands';
import {
  AllocationStatus,
  SignalScope,
  type ChangeStatusCommand,
  type SignalDto,
} from '@/api/generated/schemas';
import { useAccess } from '@/auth/access';
import { feedOf, queueOf, sweepStatus, verdictOf, type SweepStatus } from './dashboardModel';

const FEED_MAX = 4;

// POST /api/plan/commands needs the System.Text.Json discriminator `kind`, which
// Swashbuckle omits from the schema (so orval's command types lack it). Injected
// in one place, never inline — mirrors useLaneActions.kinded() and
// CoverPopover.kinded().
function kinded<T extends object>(kind: 'changeStatus', body: T): T {
  return { kind, ...body } as T;
}

export type Dashboard = ReturnType<typeof useDashboard>;

export function useDashboard(scope: SignalScope) {
  const queryClient = useQueryClient();
  const access = useAccess();

  const signalsQ = useSignalsGet({ scope });
  const changesQ = useSignalsGetChanges({ scope, max: FEED_MAX });
  const sweepQ = useSignalsGetSweep();

  const acknowledge = useSignalsAcknowledge();
  const reopen = useSignalsReopen();
  const recordVisit = useSignalsRecordVisit();
  const planCommand = usePlanCommandsExecute();

  const signals = useMemo<SignalDto[]>(() => signalsQ.data ?? [], [signalsQ.data]);
  const budget = sweepQ.data?.queueBudget ?? 7;

  const refresh = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: getSignalsGetQueryKey() }),
      queryClient.invalidateQueries({ queryKey: getSignalsGetChangesQueryKey() }),
      queryClient.invalidateQueries({ queryKey: getSignalsGetSweepQueryKey() }),
    ]);
  }, [queryClient]);

  // The visit marker moves ONCE per mount, after the queue has actually been
  // rendered — and deliberately without invalidating anything. Recording the
  // visit is what makes rows stop being "unseen"; refetching immediately
  // afterwards would clear every blue dot before the user could see it, which is
  // the one thing the marker exists to prevent.
  const visitRecorded = useRef(false);
  useEffect(() => {
    if (visitRecorded.current || signalsQ.isLoading) return;
    visitRecorded.current = true;
    recordVisit.mutate();
    // recordVisit is a stable mutation object; depending on it would re-fire.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [signalsQ.isLoading]);

  const acknowledgeSignal = useCallback(
    async (id: string, reason: string | null) => {
      await acknowledge.mutateAsync({ id, data: { reason: reason || null } });
      await refresh();
    },
    [acknowledge, refresh],
  );

  const reopenSignal = useCallback(
    async (id: string) => {
      await reopen.mutateAsync({ id });
      await refresh();
    },
    [reopen, refresh],
  );

  // The single inline gesture (ADR-0032 §3): a state change that moves no hours.
  // It does NOT resolve the signal — it mutates the plan through the envelope and
  // lets the detector observe that the condition is gone. Two writers of the same
  // truth would eventually disagree.
  const confirmTentative = useCallback(
    async (allocationId: string) => {
      await planCommand.mutateAsync({
        data: kinded<ChangeStatusCommand>('changeStatus', {
          id: allocationId,
          status: AllocationStatus.Hard,
        }),
      });
      // The mutation ripples into every coverage-derived board read, not just the
      // queue: leaving those stale would show a confirmed block as tentative the
      // moment the user follows the deep link.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getAllocationsGetInRangeQueryKey() }),
        queryClient.invalidateQueries({ queryKey: getLoadGetDemandCoverageInRangeQueryKey() }),
        queryClient.invalidateQueries({ queryKey: getLoadGetResourceLoadProfilesQueryKey() }),
      ]);
      await refresh();
    },
    [planCommand, queryClient, refresh],
  );

  const verdict = useMemo(() => verdictOf(signals), [signals]);
  const queue = useMemo(() => queueOf(signals, budget), [signals, budget]);
  const feed = useMemo(() => feedOf(changesQ.data ?? [], FEED_MAX), [changesQ.data]);

  const status: SweepStatus = useMemo(
    () => sweepStatus(sweepQ.data, new Date()),
    [sweepQ.data],
  );

  return {
    isLoading: signalsQ.isLoading || sweepQ.isLoading,
    isError: signalsQ.isError || sweepQ.isError,
    isMutating: acknowledge.isPending || reopen.isPending || planCommand.isPending,

    verdict,
    queue,
    feed,
    status,
    sweep: sweepQ.data,
    lastVisitedAt: sweepQ.data?.lastVisitedAt ?? null,

    canPlan: access.can('plan'),

    acknowledgeSignal,
    reopenSignal,
    confirmTentative,
    refresh,
  };
}
