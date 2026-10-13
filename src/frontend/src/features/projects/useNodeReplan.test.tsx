import { describe, expect, it } from 'vitest';
import { act, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import type { PlanCommandResult } from '@/api/generated/schemas';
import { renderHookWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { useNodeReplan } from './useNodeReplan';
import type { ReplanGesture } from './replanModel';

const gesture: ReplanGesture = {
  projectId: 'p1',
  nodeId: 'f2',
  nodeName: 'Fase 2 · Build',
  edge: 'end',
  oldFrom: '2026-07-01',
  oldTo: '2026-08-30',
  newFrom: '2026-07-01',
  newTo: '2026-09-09',
};

type Sent = { kind: string; dryRun: boolean; plannedEnd: string };

// Answers every plan command with `result`, echoing dryRun, and records what
// was sent — the sequence of calls IS the behaviour under test.
function planCommands(result: PlanCommandResult): Sent[] {
  const sent: Sent[] = [];
  server.use(
    http.post('*/api/plan/commands', async ({ request }) => {
      const body = (await request.json()) as Sent;
      sent.push(body);
      return HttpResponse.json({ ...result, dryRun: body.dryRun, committed: !body.dryRun });
    }),
  );
  return sent;
}

const quiet: PlanCommandResult = {
  commandKind: 'replanNode',
  changes: [{ id: 'b1' }],
  consequence: { touchesFrozen: false, people: [], demands: [] },
};

const loud: PlanCommandResult = {
  commandKind: 'replanNode',
  changes: [{ id: 'b1' }],
  consequence: {
    touchesFrozen: false,
    people: [],
    demands: [{ demandId: 'd1', roleName: 'Dev senior', gapBefore: '1.09:36:00', gapAfter: '00:00:00', stateChanged: true }],
  },
};

describe('useNodeReplan', () => {
  it('nothing changes state: one dryRun, then the commit, and a receipt', async () => {
    const sent = planCommands(quiet);
    const { result } = renderHookWithProviders(() => useNodeReplan());

    act(() => result.current.start(gesture));

    await waitFor(() => expect(result.current.state.phase).toBe('done'));
    expect(sent.map((c) => [c.kind, c.dryRun])).toEqual([
      ['replanNode', true],
      ['replanNode', false],
    ]);
    expect(result.current.preview).toBeNull();
  });

  it('a state change stops at the bar and commits only on accept', async () => {
    const sent = planCommands(loud);
    const { result } = renderHookWithProviders(() => useNodeReplan());

    act(() => result.current.start(gesture));

    await waitFor(() => expect(result.current.state.phase).toBe('pending'));
    expect(sent).toHaveLength(1);
    expect(sent[0]!.dryRun).toBe(true);
    // The ghost stays on the board while the bar is asking.
    expect(result.current.preview).toMatchObject({ nodeId: 'f2', edge: 'end', newTo: '2026-09-09' });

    act(() => result.current.accept());

    await waitFor(() => expect(result.current.state.phase).toBe('done'));
    expect(sent.map((c) => c.dryRun)).toEqual([true, false]);
  });

  it('cancelling the bar commits nothing', async () => {
    const sent = planCommands(loud);
    const { result } = renderHookWithProviders(() => useNodeReplan());

    act(() => result.current.start(gesture));
    await waitFor(() => expect(result.current.state.phase).toBe('pending'));
    act(() => result.current.dismiss());

    expect(result.current.state.phase).toBe('idle');
    expect(sent).toHaveLength(1);
  });

  it('undo sends the same node back to where it was', async () => {
    const sent = planCommands(quiet);
    const { result } = renderHookWithProviders(() => useNodeReplan());

    act(() => result.current.start(gesture));
    await waitFor(() => expect(result.current.state.phase).toBe('done'));
    act(() => result.current.undo());

    await waitFor(() => expect(sent).toHaveLength(3));
    expect(sent[2]).toMatchObject({ kind: 'replanNode', dryRun: false, plannedEnd: '2026-08-30' });
    expect(result.current.state.phase).toBe('idle');
  });

  it('an edge released where it started asks nothing', () => {
    const sent = planCommands(quiet);
    const { result } = renderHookWithProviders(() => useNodeReplan());

    act(() => result.current.start({ ...gesture, newTo: gesture.oldTo }));

    expect(result.current.state.phase).toBe('idle');
    expect(sent).toHaveLength(0);
  });

  it('a refusal applies nothing and leaves no ghost behind', async () => {
    server.use(
      http.post('*/api/plan/commands', () =>
        HttpResponse.json({ title: 'Conflict', detail: 'Coverage cannot follow.' }, { status: 409 }),
      ),
    );
    const { result } = renderHookWithProviders(() => useNodeReplan());

    act(() => result.current.start(gesture));

    await waitFor(() => expect(result.current.state.phase).toBe('idle'));
    expect(result.current.preview).toBeNull();
  });
});
