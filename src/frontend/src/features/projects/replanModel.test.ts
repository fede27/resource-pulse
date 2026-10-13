import { describe, expect, it } from 'vitest';
import type { PlanConsequence } from '@/api/generated/schemas';
import {
  deltaDays,
  directionOf,
  dragDate,
  edgeBounds,
  inverseOf,
  isNoop,
  movedDate,
  replanCommand,
  stateChangesOf,
  withEdgeAt,
  type ReplanGesture,
} from './replanModel';

const gesture = (over: Partial<ReplanGesture> = {}): ReplanGesture => ({
  projectId: 'p1',
  nodeId: 'f2',
  nodeName: 'Fase 2 · Build',
  edge: 'end',
  oldFrom: '2026-07-01',
  oldTo: '2026-08-30',
  newFrom: '2026-07-01',
  newTo: '2026-09-09',
  ...over,
});

describe('the gesture', () => {
  it('reads the moved edge, its delta and its direction', () => {
    const g = gesture();
    expect(movedDate(g)).toEqual({ before: '2026-08-30', after: '2026-09-09' });
    expect(deltaDays(g)).toBe(10);
    expect(directionOf(g)).toBe('lengthen');
    expect(directionOf(gesture({ newTo: '2026-08-16' }))).toBe('shorten');
  });

  it('an EARLIER start lengthens the node, a later one shortens it', () => {
    const earlier = gesture({ edge: 'start', newFrom: '2026-06-24', newTo: '2026-08-30' });
    const later = gesture({ edge: 'start', newFrom: '2026-07-08', newTo: '2026-08-30' });
    expect(deltaDays(earlier)).toBe(-7);
    expect(directionOf(earlier)).toBe('lengthen');
    expect(directionOf(later)).toBe('shorten');
  });

  it('an edge released where it started is not a gesture', () => {
    expect(isNoop(gesture({ newTo: '2026-08-30' }))).toBe(true);
    expect(isNoop(gesture())).toBe(false);
  });

  it('snaps the drag to whole days', () => {
    const base = { edge: 'end' as const, oldFrom: '2026-07-01', oldTo: '2026-08-30' };
    expect(dragDate(base, 134, 13.4)).toBe('2026-09-09');
    expect(dragDate(base, 5, 13.4)).toBe('2026-08-30');
    expect(dragDate(base, -140, 13.4)).toBe('2026-08-20');
  });

  it('never drags an edge past the other one', () => {
    const end = { edge: 'end' as const, oldFrom: '2026-07-01', oldTo: '2026-08-30' };
    const start = { edge: 'start' as const, oldFrom: '2026-07-01', oldTo: '2026-08-30' };
    expect(dragDate(end, -5000, 13.4)).toBe('2026-07-01');
    expect(dragDate(start, 5000, 13.4)).toBe('2026-08-30');
  });

  it('a phase edge stops at its project', () => {
    const project = { from: '2026-12-01', to: '2026-12-31' };
    const phase = { from: '2026-12-01', to: '2026-12-10' };
    const start = edgeBounds(phase, 'start', project, []);
    const end = edgeBounds({ from: '2026-12-21', to: '2026-12-31' }, 'end', project, []);

    expect(dragDate({ edge: 'start', oldFrom: phase.from, oldTo: phase.to }, -500, 13.4, start)).toBe('2026-12-01');
    expect(dragDate({ edge: 'end', oldFrom: '2026-12-21', oldTo: '2026-12-31' }, 500, 13.4, end)).toBe('2026-12-31');
  });

  it('a project edge stops at its phases', () => {
    const project = { from: '2026-12-01', to: '2026-12-31' };
    const phases = [
      { from: '2026-12-05', to: '2026-12-10' },
      { from: '2026-12-11', to: '2026-12-20' },
    ];
    expect(edgeBounds(project, 'start', null, phases)).toEqual({ max: '2026-12-05' });
    expect(edgeBounds(project, 'end', null, phases)).toEqual({ min: '2026-12-20' });
    expect(edgeBounds(project, 'end', null, [])).toEqual({});
  });

  it('a phase already outside its project may come back in, never go further out', () => {
    const project = { from: '2026-12-01', to: '2026-12-31' };
    const stray = { from: '2026-11-27', to: '2026-12-10' };
    const bounds = edgeBounds(stray, 'start', project, []);
    const base = { edge: 'start' as const, oldFrom: stray.from, oldTo: stray.to };

    expect(dragDate(base, -134, 13.4, bounds)).toBe('2026-11-27');
    expect(dragDate(base, 13.4, 13.4, bounds)).toBe('2026-11-28');
    expect(dragDate(base, 13.4 * 4, 13.4, bounds)).toBe('2026-12-01');
  });

  it('moves only the dragged edge', () => {
    expect(withEdgeAt({ edge: 'end', oldFrom: 'a', oldTo: 'b' }, 'x')).toEqual({ newFrom: 'a', newTo: 'x' });
    expect(withEdgeAt({ edge: 'start', oldFrom: 'a', oldTo: 'b' }, 'x')).toEqual({ newFrom: 'x', newTo: 'b' });
  });

  it('sends the whole window, so the edge that did not move is not cleared', () => {
    expect(replanCommand(gesture(), true)).toEqual({
      nodeId: 'f2',
      plannedStart: '2026-07-01',
      plannedEnd: '2026-09-09',
      dryRun: true,
    });
  });

  it('undo is the same gesture backwards', () => {
    const back = inverseOf(gesture());
    expect(back.oldTo).toBe('2026-09-09');
    expect(back.newTo).toBe('2026-08-30');
    expect(replanCommand(back, false).plannedEnd).toBe('2026-08-30');
  });
});

describe('stateChangesOf', () => {
  // Design case 1b: Marco into overload, Dev senior covered, Luca into Full.
  const consequence: PlanConsequence = {
    today: '2026-06-22',
    frozenUntil: '2026-07-06',
    touchesFrozen: false,
    people: [
      {
        resourceId: 'luca',
        resourceName: 'Luca Ferri',
        hoursDelta: '1.14:24:00',
        intervals: [
          {
            from: '2026-08-31',
            to: '2026-09-09',
            percentBefore: 30,
            percentAfter: 90,
            bandChanged: true,
            overloadedAfter: false,
            otherProjects: [{ id: 'x', name: 'Intranet Regione' }],
          },
        ],
      },
      {
        resourceId: 'marco',
        resourceName: 'Marco Rossi',
        hoursDelta: '2.03:12:00',
        intervals: [
          {
            from: '2026-08-31',
            to: '2026-09-09',
            percentBefore: 40,
            percentAfter: 120,
            bandChanged: true,
            overloadedAfter: true,
            otherProjects: [{ id: 'x', name: 'Intranet Regione' }],
          },
        ],
      },
    ],
    demands: [
      {
        demandId: 'd-senior',
        roleName: 'Dev senior',
        nodeName: 'Fase 2',
        requiredHours: '10.00:00:00',
        gapBefore: '1.09:36:00',
        gapAfter: '-04:48:00',
        stateChanged: true,
      },
      {
        demandId: 'd-dev',
        roleName: 'Dev',
        nodeName: 'Fase 2',
        requiredHours: '10.20:00:00',
        gapBefore: '-15:12:00',
        gapAfter: '-2.18:24:00',
        stateChanged: false,
      },
    ],
  };

  it('orders overload first, then demands, then the other bands', () => {
    const rows = stateChangesOf(consequence);
    expect(rows.map((r) => r.kind)).toEqual(['load', 'demand', 'load']);
    expect(rows[0]).toMatchObject({ resourceName: 'Marco Rossi', percentAfter: 120, overloaded: true });
    expect(rows[2]).toMatchObject({ resourceName: 'Luca Ferri', percentAfter: 90, overloaded: false });
  });

  it('a demand that was already covered is not a row', () => {
    const demands = stateChangesOf(consequence).filter((r) => r.kind === 'demand');
    expect(demands).toHaveLength(1);
    expect(demands[0]).toMatchObject({ roleName: 'Dev senior', gapBefore: 33.6, gapAfter: 0, covered: true });
  });

  it('a surplus reads as zero uncovered hours, never as a negative gap', () => {
    const reopened: PlanConsequence = {
      demands: [{ demandId: 'd', roleName: 'Dev', gapBefore: '-15:12:00', gapAfter: '2.00:48:00', stateChanged: true }],
    };
    expect(stateChangesOf(reopened)[0]).toMatchObject({ gapBefore: 0, gapAfter: 48.8, covered: false });
  });

  it('the frozen zone comes before everything else', () => {
    const rows = stateChangesOf({ ...consequence, touchesFrozen: true });
    expect(rows[0]).toEqual({ kind: 'frozen', from: '2026-06-22', to: '2026-07-06' });
  });

  it('a rate that moves inside one band is in the account, not in the bar', () => {
    const quiet: PlanConsequence = {
      people: [
        {
          resourceId: 'luca',
          resourceName: 'Luca Ferri',
          intervals: [{ from: '2026-08-31', to: '2026-09-09', percentBefore: 50, percentAfter: 70, bandChanged: false }],
        },
      ],
      demands: [{ demandId: 'd', roleName: 'Dev', gapBefore: '7.03:12:00', gapAfter: '6.14:24:00', stateChanged: false }],
    };
    expect(stateChangesOf(quiet)).toEqual([]);
  });

  it('hours beyond the availability ride on the row of the interval they fall in', () => {
    const sara: PlanConsequence = {
      people: [
        {
          resourceId: 'sara',
          resourceName: 'Sara Conti',
          availableUntil: '2026-09-30',
          beyondAvailability: { from: '2026-10-01', to: '2026-10-02', days: 2, hours: '09:36:00' },
          intervals: [
            { from: '2026-09-28', to: '2026-10-02', percentBefore: 0, percentAfter: 60, bandChanged: true },
          ],
        },
      ],
    };
    expect(stateChangesOf(sara)[0]).toMatchObject({
      kind: 'load',
      beyondAvailability: { from: '2026-10-01', to: '2026-10-02', boundary: '2026-09-30' },
    });
  });

  it('no consequence, no rows', () => {
    expect(stateChangesOf(null)).toEqual([]);
    expect(stateChangesOf(undefined)).toEqual([]);
  });
});
