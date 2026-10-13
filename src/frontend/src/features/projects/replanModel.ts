// Moving a node on the Projects board — pure view-model (ADR-0035 §5, ADR-0036).
//
// The gesture is `replanNode`; what it costs comes back from the SAME command's
// dryRun as a `PlanConsequence`. This module turns that payload into the rows
// the confirmation shows. It decides nothing: which transitions are state
// changes is flagged by the server (`touchesFrozen`, `bandChanged`,
// `stateChanged`), so the board and triage cannot disagree about what counts.

import dayjs from 'dayjs';
import type { PlanCommandResult, PlanConsequence, ReplanNodeCommand } from '@/api/generated/schemas';
import { parseDurationHours } from '@/lib/duration';

const ISO = 'YYYY-MM-DD';

export type ReplanEdge = 'start' | 'end';

// One edge of one node, dragged from its planned date to a new one.
export type ReplanGesture = {
  projectId: string;
  nodeId: string;
  nodeName: string;
  edge: ReplanEdge;
  oldFrom: string;
  oldTo: string;
  newFrom: string;
  newTo: string;
};

// What the row draws while an edge is in flight, and after release until the
// gesture is committed or cancelled.
export type ReplanPreview = Pick<ReplanGesture, 'projectId' | 'nodeId' | 'edge' | 'newFrom' | 'newTo'>;

export type ReplanDirection = 'lengthen' | 'shorten';

export function movedDate(g: ReplanGesture): { before: string; after: string } {
  return g.edge === 'start' ? { before: g.oldFrom, after: g.newFrom } : { before: g.oldTo, after: g.newTo };
}

// Signed calendar days the edge moved (positive = later).
export function deltaDays(g: ReplanGesture): number {
  const { before, after } = movedDate(g);
  return dayjs(after).diff(dayjs(before), 'day');
}

// A later end or an earlier start makes the node longer.
export function directionOf(g: ReplanGesture): ReplanDirection {
  const d = deltaDays(g);
  return (g.edge === 'end' ? d > 0 : d < 0) ? 'lengthen' : 'shorten';
}

export const isNoop = (g: ReplanGesture): boolean => deltaDays(g) === 0;

// How far an edge may travel: a phase stays inside its project, and a project
// keeps containing its phases.
export type EdgeBounds = { min?: string; max?: string };

type Window = { from: string; to: string };

// The bounds of one edge of `node` on the board. `parent` is the project for a
// phase and null for the project itself; `children` are the project's phases.
//
// A bound never forces a move: where a node ALREADY breaks containment (the
// server does not enforce it, so such data can exist), the edge may not go
// further out but is free to come back in. A nudge is never turned into a jump.
export function edgeBounds(
  node: Window,
  edge: ReplanEdge,
  parent: Window | null,
  children: ReadonlyArray<Window>,
): EdgeBounds {
  const earliest = (a: string, b: string) => (a < b ? a : b);
  const latest = (a: string, b: string) => (a > b ? a : b);
  if (parent) {
    return edge === 'start'
      ? { min: earliest(parent.from, node.from) }
      : { max: latest(parent.to, node.to) };
  }
  if (children.length === 0) return {};
  return edge === 'start'
    ? { max: latest(children.map((c) => c.from).reduce(earliest), node.from) }
    : { min: earliest(children.map((c) => c.to).reduce(latest), node.to) };
}

// The date an edge lands on after `dxPx` of drag, snapped to whole days, kept
// on its own side of the other edge — a node is never inverted — and inside
// `bounds`.
export function dragDate(
  g: Pick<ReplanGesture, 'edge' | 'oldFrom' | 'oldTo'>,
  dxPx: number,
  dayPx: number,
  bounds: EdgeBounds = {},
): string {
  const days = Math.round(dxPx / dayPx);
  const origin = g.edge === 'end' ? g.oldTo : g.oldFrom;
  let next = dayjs(origin).add(days, 'day').format(ISO);
  if (g.edge === 'end' && next < g.oldFrom) next = g.oldFrom;
  if (g.edge === 'start' && next > g.oldTo) next = g.oldTo;
  if (bounds.min && next < bounds.min) next = bounds.min;
  if (bounds.max && next > bounds.max) next = bounds.max;
  return next;
}

export function withEdgeAt(
  g: Pick<ReplanGesture, 'edge' | 'oldFrom' | 'oldTo'>,
  date: string,
): { newFrom: string; newTo: string } {
  return g.edge === 'end' ? { newFrom: g.oldFrom, newTo: date } : { newFrom: date, newTo: g.oldTo };
}

export function replanCommand(g: ReplanGesture, dryRun: boolean): ReplanNodeCommand {
  return { nodeId: g.nodeId, plannedStart: g.newFrom, plannedEnd: g.newTo, dryRun };
}

// Undo is the same gesture backwards. It is exact because propagation is a
// re-snap: the edges that followed the node out follow it back, and the pinned
// ones never moved.
export function inverseOf(g: ReplanGesture): ReplanGesture {
  return { ...g, oldFrom: g.newFrom, oldTo: g.newTo, newFrom: g.oldFrom, newTo: g.oldTo };
}

// ── State changes: the only things the confirmation shows ────────────────

export type StateChange =
  | { kind: 'frozen'; from: string; to: string }
  | {
      kind: 'load';
      resourceId: string;
      resourceName: string;
      from: string;
      to: string;
      percentBefore: number;
      percentAfter: number;
      overloaded: boolean;
      otherProjects: string[];
      // Set when part of this interval lies outside the person's declared
      // availability: a circumstance of the row, never a row of its own.
      beyondAvailability: { from: string; to: string; boundary: string } | null;
    }
  | {
      kind: 'demand';
      demandId: string;
      roleName: string;
      nodeName: string;
      gapBefore: number;
      gapAfter: number;
      covered: boolean;
    };

const round1 = (n: number) => Math.round(n * 10) / 10;

// The uncovered hours a gap reads as: a surplus is "0 uncovered", not negative.
const uncovered = (gap: string | null | undefined): number => Math.max(0, round1(parseDurationHours(gap)));

// Order is the design's: frozen first, then overload, then demands, then the
// other band transitions.
export function stateChangesOf(c: PlanConsequence | null | undefined): StateChange[] {
  if (!c) return [];
  const frozen: StateChange[] = c.touchesFrozen
    ? [{ kind: 'frozen', from: c.today ?? '', to: c.frozenUntil ?? '' }]
    : [];

  const loads: Extract<StateChange, { kind: 'load' }>[] = [];
  for (const p of c.people ?? []) {
    for (const i of p.intervals ?? []) {
      if (!i.bandChanged) continue;
      const from = i.from ?? '';
      const to = i.to ?? '';
      const b = p.beyondAvailability;
      const overlaps = !!b && (b.from ?? '') <= to && (b.to ?? '') >= from;
      loads.push({
        kind: 'load',
        resourceId: p.resourceId ?? '',
        resourceName: p.resourceName ?? '—',
        from,
        to,
        percentBefore: i.percentBefore ?? 0,
        percentAfter: i.percentAfter ?? 0,
        overloaded: i.overloadedAfter ?? false,
        otherProjects: (i.otherProjects ?? []).map((o) => o.name ?? '').filter(Boolean),
        beyondAvailability:
          overlaps && b
            ? {
                from: b.from ?? '',
                to: b.to ?? '',
                // Whichever declared boundary the added days fall outside of.
                boundary:
                  p.availableUntil && (b.to ?? '') > p.availableUntil
                    ? p.availableUntil
                    : (p.availableFrom ?? p.availableUntil ?? ''),
              }
            : null,
      });
    }
  }

  const demands: StateChange[] = (c.demands ?? [])
    .filter((d) => d.stateChanged)
    .map((d) => {
      const gapAfter = uncovered(d.gapAfter);
      return {
        kind: 'demand' as const,
        demandId: d.demandId ?? '',
        roleName: d.roleName ?? '—',
        nodeName: d.nodeName ?? '',
        gapBefore: uncovered(d.gapBefore),
        gapAfter,
        covered: gapAfter === 0,
      };
    });

  return [
    ...frozen,
    ...loads.filter((l) => l.overloaded),
    ...demands,
    ...loads.filter((l) => !l.overloaded),
  ];
}

export const movedBlockCount = (r: PlanCommandResult | null | undefined): number => r?.changes?.length ?? 0;

// Signed hours, one decimal — the account's unit.
export const hoursOf = (span: string | null | undefined): number => round1(parseDurationHours(span));
