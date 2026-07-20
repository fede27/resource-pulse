import { useCallback, useEffect, useRef, useState } from 'react';
import { App } from 'antd';
import { AxiosError } from 'axios';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { getAllocationsGetInRangeQueryKey } from '@/api/generated/allocations/allocations';
import {
  getLoadGetDemandCoverageInRangeQueryKey,
  getLoadGetResourceLoadProfilesQueryKey,
} from '@/api/generated/load/load';
import { getProjectNodesGetAllQueryKey } from '@/api/generated/project-nodes/project-nodes';
import {
  getProjectsGetActiveInRangeQueryKey,
  useProjectsCancel,
  useProjectsComplete,
  useProjectsResume,
  useProjectsStart,
  useProjectsSuspend,
  useProjectsUpdateProject,
} from '@/api/generated/projects/projects';
import type { CommitmentLevel } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import type { BoardProject, ProjectAction } from './boardModel';

export type ReasonActionKind = 'suspend' | 'cancel';
export type ReasonModalState = { action: ReasonActionKind; project: BoardProject } | null;

// The backend Conflict for a commitment downgrade across the hard threshold
// (ADR-0015 §4) — anything else on 409 is a plain conflict for the error funnel.
const DEMOTE_CONFLICT = /(\d+) Hard allocation/;

// Orchestrates the contextual actions (kebab) on a project root: lifecycle
// transitions and commitment change. Confirm modals for the one-way gestures,
// a reason modal for suspend/cancel (the domain requires the reason), and the
// Conflict → confirm → retry flow for the hard-allocation cascade demotion.
export function useProjectActions() {
  const { t } = useTranslation();
  const { message, modal } = App.useApp();
  const showApiError = useApiError();
  const queryClient = useQueryClient();

  const start = useProjectsStart();
  const complete = useProjectsComplete();
  const suspend = useProjectsSuspend();
  const resume = useProjectsResume();
  const cancel = useProjectsCancel();
  const update = useProjectsUpdateProject();

  const [reasonModal, setReasonModal] = useState<ReasonModalState>(null);
  const [reasonSubmitting, setReasonSubmitting] = useState(false);

  // Status changes ripple into every board read: roots (status), demand
  // coverage (I4 excludes Closed/Cancelled), and — for the commitment cascade
  // demotion — allocations and hard-only profiles. Unparameterized keys are
  // prefixes: they match every fetched range.
  const invalidateBoard = () => {
    void queryClient.invalidateQueries({ queryKey: getProjectsGetActiveInRangeQueryKey() });
    void queryClient.invalidateQueries({ queryKey: getProjectNodesGetAllQueryKey() });
    void queryClient.invalidateQueries({ queryKey: getLoadGetDemandCoverageInRangeQueryKey() });
    void queryClient.invalidateQueries({ queryKey: getAllocationsGetInRangeQueryKey() });
    void queryClient.invalidateQueries({ queryKey: getLoadGetResourceLoadProfilesQueryKey() });
  };

  const applyCommitment = async (project: BoardProject, level: CommitmentLevel, confirmDemote: boolean) => {
    try {
      await update.mutateAsync({
        id: project.id,
        // Full-replace PUT: send the current type/lead/client back untouched.
        data: {
          type: project.type,
          commitmentLevel: level,
          leadResourceId: project.ownerId,
          client: project.client,
          confirmDemoteHardAllocations: confirmDemote,
        },
      });
      message.success(t('projects.actions.commitmentSuccess', { name: project.name }));
      invalidateBoard();
    } catch (e) {
      const detail =
        e instanceof AxiosError && e.response?.status === 409
          ? ((e.response.data as { detail?: string } | undefined)?.detail ?? '')
          : '';
      const demote = DEMOTE_CONFLICT.exec(detail);
      if (!confirmDemote && demote) {
        modal.confirm({
          title: t('projects.actions.demoteTitle', { name: project.name }),
          content: t('projects.actions.demoteBody', { n: Number(demote[1]) }),
          okText: t('projects.actions.demoteCta'),
          okButtonProps: { danger: true },
          onOk: () => applyCommitment(project, level, true),
        });
        return;
      }
      showApiError(e);
    }
  };

  const impl = (project: BoardProject, action: ProjectAction) => {
    switch (action.kind) {
      case 'start':
        modal.confirm({
          title: t('projects.actions.startConfirmTitle', { name: project.name }),
          content: t('projects.actions.startConfirmBody'),
          okText: t('projects.actions.startCta'),
          onOk: async () => {
            try {
              await start.mutateAsync({ id: project.id });
              message.success(t('projects.actions.startSuccess', { name: project.name }));
              invalidateBoard();
            } catch (e) {
              showApiError(e);
            }
          },
        });
        break;
      case 'complete':
        modal.confirm({
          title: t('projects.actions.completeConfirmTitle', { name: project.name }),
          content: t('projects.actions.completeConfirmBody'),
          okText: t('projects.actions.completeCta'),
          onOk: async () => {
            try {
              await complete.mutateAsync({ id: project.id });
              message.success(t('projects.actions.completeSuccess', { name: project.name }));
              invalidateBoard();
            } catch (e) {
              showApiError(e);
            }
          },
        });
        break;
      case 'resume':
        void resume
          .mutateAsync({ id: project.id })
          .then(() => {
            message.success(t('projects.actions.resumeSuccess', { name: project.name }));
            invalidateBoard();
          })
          .catch((e: unknown) => showApiError(e));
        break;
      case 'suspend':
      case 'cancel':
        setReasonModal({ action: action.kind, project });
        break;
      case 'setCommitment':
        if (action.level === project.commitmentLevel) return;
        void applyCommitment(project, action.level, false);
        break;
    }
  };

  // Stable identity for memoized rows: the callback the rows receive never
  // changes; it always dispatches to the latest closure (updated post-render —
  // fine, it is only ever invoked from user events).
  const implRef = useRef(impl);
  useEffect(() => {
    implRef.current = impl;
  });
  const run = useCallback((project: BoardProject, action: ProjectAction) => {
    implRef.current(project, action);
  }, []);

  const onReasonSubmit = async (reason: string) => {
    if (!reasonModal) return;
    const { action, project } = reasonModal;
    setReasonSubmitting(true);
    try {
      if (action === 'suspend') {
        await suspend.mutateAsync({ id: project.id, data: { reason } });
        message.success(t('projects.actions.suspendSuccess', { name: project.name }));
      } else {
        await cancel.mutateAsync({ id: project.id, data: { reason } });
        message.success(t('projects.actions.cancelSuccess', { name: project.name }));
      }
      invalidateBoard();
      setReasonModal(null);
    } catch (e) {
      showApiError(e);
    } finally {
      setReasonSubmitting(false);
    }
  };

  return {
    run,
    reasonModal,
    reasonSubmitting,
    onReasonSubmit,
    onReasonCancel: () => setReasonModal(null),
  };
}
