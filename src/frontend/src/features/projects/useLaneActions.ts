import { useCallback, useEffect, useRef, useState } from 'react';
import { App } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { getAllocationsGetInRangeQueryKey } from '@/api/generated/allocations/allocations';
import {
  getLoadGetDemandCoverageInRangeQueryKey,
  getLoadGetOpenDemandsQueryKey,
  getLoadGetResourceLoadProfilesQueryKey,
} from '@/api/generated/load/load';
import { usePlanCommandsExecute } from '@/api/generated/plan-commands/plan-commands';
import {
  AllocationStatus,
  type ChangeStatusCommand,
  type CreateCommand,
  type DeleteCommand,
  type DeleteDemandCommand,
  type EditDemandCommand,
  type ReassignCommand,
  type RetargetCommand,
} from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import type { BoardProject, CoverageBlock, DemandRow, LaneAction } from './boardModel';

// The wire format of POST /api/plan/commands needs the System.Text.Json
// discriminator `kind`, which Swashbuckle omits from the schema (so orval's
// command types lack it) — injected here in one place, never inline at call
// sites. Mirrors CoverPopover.kinded() on the people board.
type LaneKind =
  | 'changeStatus'
  | 'delete'
  | 'reassign'
  | 'retarget'
  | 'create'
  | 'editDemand'
  | 'deleteDemand';
function kinded<T extends object>(kind: LaneKind, body: T): T {
  return { kind, ...body } as T;
}

// Whole-hours → TimeSpan for the demand's RequiredHours (best-effort = null).
// The plan-command envelope deserializes TimeSpan in the .NET CONSTANT format
// ([d.]hh:mm:ss), NOT ISO-8601 — "PT60H" is rejected on input (learned in F4).
// The board reads demand hours as integers, so minutes/seconds are always 00.
const hoursToTimeSpan = (h: number | null): string | null => {
  if (h === null) return null;
  const total = Math.max(0, Math.round(h));
  const hh = String(total % 24).padStart(2, '0');
  const days = Math.floor(total / 24);
  return days > 0 ? `${days}.${hh}:00:00` : `${hh}:00:00`;
};

export type LaneModalState =
  | { kind: 'reassign'; block: CoverageBlock; project: BoardProject }
  | { kind: 'retarget'; block: CoverageBlock; project: BoardProject }
  | { kind: 'cover'; demand: DemandRow; project: BoardProject }
  | { kind: 'editDemand'; demand: DemandRow; project: BoardProject }
  | null;

export type EditDemandPatch = {
  roleId: string;
  requiredHours: number | null;
  ownerResourceId: string | null;
};

// Orchestrates the contextual actions on the expanded coverage / open-role
// lanes. In-place reversible gestures (promote/demote) commit directly; the
// destructive ones (remove coverage, delete demand) go through a confirm; the
// ones that need input (reassign, retarget, cover, edit demand) open a modal
// whose submit lands here. Every mutation is a single plan-command.
export function useLaneActions() {
  const { t } = useTranslation();
  const { message, modal } = App.useApp();
  const showApiError = useApiError();
  const queryClient = useQueryClient();

  const execute = usePlanCommandsExecute();

  const [laneModal, setLaneModal] = useState<LaneModalState>(null);
  const [laneSubmitting, setLaneSubmitting] = useState(false);

  // A plan mutation ripples into every coverage-derived board read: the
  // allocation slice, the demand reconciliation (gap), the hard-only peaks, and
  // the cross-project open-demands picker. Unparameterized keys are prefixes.
  const invalidateBoard = () => {
    void queryClient.invalidateQueries({ queryKey: getAllocationsGetInRangeQueryKey() });
    void queryClient.invalidateQueries({ queryKey: getLoadGetDemandCoverageInRangeQueryKey() });
    void queryClient.invalidateQueries({ queryKey: getLoadGetResourceLoadProfilesQueryKey() });
    void queryClient.invalidateQueries({ queryKey: getLoadGetOpenDemandsQueryKey() });
  };

  // `success` is a pre-resolved message (call sites pass t('literal') so the
  // typed-t() key union isn't threaded through a plain string param).
  const commit = async <T extends object>(kind: LaneKind, body: T, success: string) => {
    try {
      await execute.mutateAsync({ data: kinded(kind, body) });
      message.success(success);
      invalidateBoard();
    } catch (e) {
      showApiError(e);
    }
  };

  const impl = (action: LaneAction) => {
    switch (action.kind) {
      case 'promote':
        void commit<ChangeStatusCommand>(
          'changeStatus',
          { id: action.block.id, status: AllocationStatus.Hard },
          t('projects.lane.actions.promoteSuccess'),
        );
        break;
      case 'demote':
        void commit<ChangeStatusCommand>(
          'changeStatus',
          { id: action.block.id, status: AllocationStatus.Tentative },
          t('projects.lane.actions.demoteSuccess'),
        );
        break;
      case 'remove':
        modal.confirm({
          title: t('projects.lane.actions.removeConfirmTitle', {
            name: action.block.resourceName,
            role: action.block.demandRoleName || '—',
          }),
          content: t('projects.lane.actions.removeConfirmBody', {
            role: action.block.demandRoleName || '—',
          }),
          okText: t('projects.lane.actions.removeCta'),
          okButtonProps: { danger: true },
          onOk: () =>
            commit<DeleteCommand>(
              'delete',
              { id: action.block.id },
              t('projects.lane.actions.removeSuccess'),
            ),
        });
        break;
      case 'deleteDemand':
        modal.confirm({
          title: t('projects.lane.actions.deleteDemandConfirmTitle', { role: action.demand.roleName }),
          content: t('projects.lane.actions.deleteDemandConfirmBody'),
          okText: t('projects.lane.actions.deleteDemandCta'),
          okButtonProps: { danger: true },
          onOk: () =>
            commit<DeleteDemandCommand>(
              'deleteDemand',
              { id: action.demand.demandId },
              t('projects.lane.actions.deleteDemandSuccess'),
            ),
        });
        break;
      case 'reassign':
      case 'retarget':
        setLaneModal({ kind: action.kind, block: action.block, project: action.project });
        break;
      case 'cover':
      case 'editDemand':
        setLaneModal({ kind: action.kind, demand: action.demand, project: action.project });
        break;
    }
  };

  // Stable identity for the memoized rows (see ProjectRow.memo): the callback
  // never changes; it dispatches to the latest closure, updated post-render.
  const implRef = useRef(impl);
  useEffect(() => {
    implRef.current = impl;
  });
  const run = useCallback((action: LaneAction) => implRef.current(action), []);

  const closeModal = () => setLaneModal(null);

  const withSubmitting = async (fn: () => Promise<void>) => {
    setLaneSubmitting(true);
    try {
      await fn();
      setLaneModal(null);
    } finally {
      setLaneSubmitting(false);
    }
  };

  const onReassignSubmit = (resourceId: string) => {
    if (laneModal?.kind !== 'reassign') return;
    const { block } = laneModal;
    void withSubmitting(() =>
      commit<ReassignCommand>(
        'reassign',
        { id: block.id, resourceId },
        t('projects.lane.actions.reassignSuccess'),
      ),
    );
  };

  const onRetargetSubmit = (demandId: string) => {
    if (laneModal?.kind !== 'retarget') return;
    const { block } = laneModal;
    void withSubmitting(() =>
      commit<RetargetCommand>(
        'retarget',
        { id: block.id, demandId },
        t('projects.lane.actions.retargetSuccess'),
      ),
    );
  };

  const onCoverSubmit = (resourceId: string, percent: number) => {
    if (laneModal?.kind !== 'cover') return;
    const { demand, project } = laneModal;
    if (!project.from || !project.to) return;
    void withSubmitting(() =>
      commit<CreateCommand>(
        'create',
        {
          demandId: demand.demandId,
          resourceId,
          periodStart: project.from!,
          periodEnd: project.to!,
          percent,
          status: AllocationStatus.Tentative,
        },
        t('projects.lane.actions.coverSuccess'),
      ),
    );
  };

  const onEditDemandSubmit = (patch: EditDemandPatch) => {
    if (laneModal?.kind !== 'editDemand') return;
    const { demand } = laneModal;
    void withSubmitting(() =>
      commit<EditDemandCommand>(
        'editDemand',
        {
          id: demand.demandId,
          roleId: patch.roleId,
          requiredHours: hoursToTimeSpan(patch.requiredHours),
          requiredHoursSet: true,
          ownerResourceId: patch.ownerResourceId,
          ownerResourceIdSet: true,
          // Notes aren't readable on the board's demand DTO — leave untouched
          // (notesSet:false) rather than risk clobbering them blind.
          notesSet: false,
        },
        t('projects.lane.actions.editDemandSuccess'),
      ),
    );
  };

  return {
    run,
    laneModal,
    laneSubmitting,
    closeModal,
    onReassignSubmit,
    onRetargetSubmit,
    onCoverSubmit,
    onEditDemandSubmit,
  };
}
