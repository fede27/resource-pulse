import { useCallback, useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { getAllocationsGetInRangeQueryKey } from '@/api/generated/allocations/allocations';
import {
  getLoadGetDemandCoverageInRangeQueryKey,
  getLoadGetOpenDemandsQueryKey,
  getLoadGetResourceLoadProfilesQueryKey,
} from '@/api/generated/load/load';
import { usePlanCommandsExecute } from '@/api/generated/plan-commands/plan-commands';
import { getProjectNodesGetAllQueryKey } from '@/api/generated/project-nodes/project-nodes';
import { getProjectsGetActiveInRangeQueryKey } from '@/api/generated/projects/projects';
import type { PlanCommandResult, ReplanNodeCommand } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import {
  inverseOf,
  isNoop,
  replanCommand,
  stateChangesOf,
  type ReplanGesture,
  type ReplanPreview,
  type StateChange,
} from './replanModel';

// Swashbuckle does not surface the System.Text.Json discriminator, so orval's
// command type lacks `kind` — injected here, as in useLaneActions.
const kinded = (body: ReplanNodeCommand): ReplanNodeCommand => ({ kind: 'replanNode', ...body }) as ReplanNodeCommand;

// The life of one node move (ADR-0036):
//   pricing   — the edge was released; ONE dryRun is reading the consequence
//   pending   — at least one state change: the bar asks before committing
//   committing— the same command, without dryRun
//   done      — committed; the receipt offers the account and the way back
export type ReplanState =
  | { phase: 'idle' }
  | { phase: 'pricing'; gesture: ReplanGesture }
  | { phase: 'pending'; gesture: ReplanGesture; result: PlanCommandResult; changes: StateChange[] }
  | { phase: 'committing'; gesture: ReplanGesture; result: PlanCommandResult; changes: StateChange[] }
  | { phase: 'done'; gesture: ReplanGesture; result: PlanCommandResult; changes: StateChange[] };

export function useNodeReplan() {
  const showApiError = useApiError();
  const queryClient = useQueryClient();
  const execute = usePlanCommandsExecute();
  const [state, setState] = useState<ReplanState>({ phase: 'idle' });

  // Awaited, not fired: the ghost stays on the board until the reads it
  // replaces have come back, so the old dates never flash in between.
  const refreshBoard = () =>
    Promise.all(
      [
        getAllocationsGetInRangeQueryKey(),
        getLoadGetDemandCoverageInRangeQueryKey(),
        getLoadGetResourceLoadProfilesQueryKey(),
        getLoadGetOpenDemandsQueryKey(),
        getProjectNodesGetAllQueryKey(),
        getProjectsGetActiveInRangeQueryKey(),
      ].map((queryKey) => queryClient.invalidateQueries({ queryKey })),
    );

  const commit = async (gesture: ReplanGesture, estimate: PlanCommandResult, changes: StateChange[]) => {
    setState({ phase: 'committing', gesture, result: estimate, changes });
    try {
      const receipt = await execute.mutateAsync({ data: kinded(replanCommand(gesture, false)) });
      await refreshBoard();
      setState({ phase: 'done', gesture, result: receipt, changes });
    } catch (e) {
      showApiError(e);
      setState({ phase: 'idle' });
    }
  };

  const startImpl = async (gesture: ReplanGesture) => {
    if (isNoop(gesture)) return;
    setState({ phase: 'pricing', gesture });
    try {
      const estimate = await execute.mutateAsync({ data: kinded(replanCommand(gesture, true)) });
      const changes = stateChangesOf(estimate.consequence);
      // Nothing changes state: the gesture is already done. The quantities
      // exist all the same, and stay behind "see the account".
      if (changes.length === 0) await commit(gesture, estimate, changes);
      else setState({ phase: 'pending', gesture, result: estimate, changes });
    } catch (e) {
      // A refusal (a re-snap that would invert a block, a root that is closed)
      // arrives here as a Conflict naming what blocks: nothing was applied.
      showApiError(e);
      setState({ phase: 'idle' });
    }
  };

  // Stable identity for the memoized rows; dispatches to the latest closure.
  const startRef = useRef(startImpl);
  useEffect(() => {
    startRef.current = startImpl;
  });
  const start = useCallback((gesture: ReplanGesture) => void startRef.current(gesture), []);

  const accept = () => {
    if (state.phase === 'pending') void commit(state.gesture, state.result, state.changes);
  };

  const dismiss = () => setState({ phase: 'idle' });

  // The same gesture backwards, straight to commit: undoing is not a decision
  // that needs pricing again.
  const undo = async () => {
    if (state.phase !== 'done') return;
    const back = inverseOf(state.gesture);
    setState({ phase: 'idle' });
    try {
      await execute.mutateAsync({ data: kinded(replanCommand(back, false)) });
      await refreshBoard();
    } catch (e) {
      showApiError(e);
    }
  };

  const preview: ReplanPreview | null =
    state.phase === 'pricing' || state.phase === 'pending' || state.phase === 'committing'
      ? {
          projectId: state.gesture.projectId,
          nodeId: state.gesture.nodeId,
          edge: state.gesture.edge,
          newFrom: state.gesture.newFrom,
          newTo: state.gesture.newTo,
        }
      : null;

  return { state, preview, start, accept, dismiss, undo: () => void undo() };
}
