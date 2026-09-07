// Dashboard — pure view-model. No React, no network.
//
// The page is a TRIAGE surface, not a reporting hub: "cosa devo guardare oggi"
// and "cosa è cambiato", never "come stiamo andando". Two rules follow, and both
// live here:
//
//   ADMISSION — every row carries a verb and a deep link with context. A row
//   that can only be looked at does not belong on this page.
//   CARDINALITY — the queue has a FIXED budget regardless of tenant size. It is
//   not a list that grows; it is a shortlist that forces triage.
//
// The ranking itself is NOT here: the server ranks (ADR-0032 §11), because it
// holds the history that explains the order. This module slices, groups and
// classifies what the server already ordered.

import {
  SignalChange,
  SignalKind,
  SignalTier,
  SignalZone,
  type SignalChangeDto,
  type SignalDto,
  type SignalSweepDto,
} from '@/api/generated/schemas';

// ── Sweep state: three states, not two ───────────────────────────────────
// An empty queue is only "the plan holds" if we know we looked. On a tenant the
// detector has never visited, that sentence would be a lie (ADR-0032 §10).
export type SweepStatus = 'never' | 'stale' | 'fresh';

export function sweepStatus(sweep: SignalSweepDto | undefined, now: Date): SweepStatus {
  if (!sweep?.lastSweptAt) return 'never';
  const ageHours = (now.getTime() - new Date(sweep.lastSweptAt).getTime()) / 3_600_000;
  return ageHours > (sweep.staleAfterHours ?? 48) ? 'stale' : 'fresh';
}

// ── Verdict ──────────────────────────────────────────────────────────────
// Text, never a gauge: a number out of context invites comparison with
// yesterday's number, which is the reporting question this page refuses.
export type Verdict = {
  clean: boolean;
  gaps: number;
  gapHours: number;
  overcommits: number;
  tentative: number;
  acknowledged: number;
};

export function verdictOf(signals: readonly SignalDto[]): Verdict {
  const of = (kind: SignalKind) => signals.filter((s) => s.kind === kind);
  const gaps = of(SignalKind.Gap);

  return {
    gaps: gaps.length,
    // Hours are the reconciliation truth (ADR-0026), so the headline figure is
    // hours — not a count of rows, which would make one 4-hour gap look like one
    // 400-hour gap.
    gapHours: Math.round(gaps.reduce((sum, s) => sum + (s.magnitude ?? 0), 0)),
    overcommits: of(SignalKind.Overcommit).length,
    tentative: of(SignalKind.TentativeInFrozen).length,
    acknowledged: signals.filter((s) => s.isAcknowledged).length,
    clean: signals.every((s) => s.tier !== SignalTier.Breach),
  };
}

// ── Queue ────────────────────────────────────────────────────────────────
export type Queue = {
  /** What the page shows: the first `budget` rows of the server's ranking. */
  shown: SignalDto[];
  /** How many exist in total — the honest "N di M". */
  total: number;
  /** Rows beyond the budget. Never rendered; stated. */
  overflow: number;
  budget: number;
};

export function queueOf(signals: readonly SignalDto[], budget: number): Queue {
  const list = [...signals];
  return {
    shown: list.slice(0, budget),
    total: list.length,
    overflow: Math.max(0, list.length - budget),
    budget,
  };
}

// ── Row presentation ─────────────────────────────────────────────────────
export type SignalTone = 'danger' | 'warning' | 'caution' | 'info' | 'neutral';

// Tone follows the KIND, not the zone: the zone already has its own stripe, and
// colouring by both would make a liquid gap look harmless when the point of the
// row is that a role is uncovered.
const TONE_BY_KIND: Partial<Record<SignalKind, SignalTone>> = {
  [SignalKind.Gap]: 'danger',
  [SignalKind.TentativeInFrozen]: 'warning',
  [SignalKind.Overcommit]: 'caution',
  [SignalKind.UnderBand]: 'info',
};

export const toneOf = (kind: SignalKind | undefined): SignalTone =>
  (kind !== undefined && TONE_BY_KIND[kind]) || 'neutral';

// The kinds travel as NUMBERS on the wire, so locale keys are indexed by a slug
// instead: "dashboard.signal.gap.title" survives a reordered enum and can be read
// in the locale file, which "dashboard.signal.1.title" cannot.
//
// The SERVER sends the parts (role, person, project) and never a sentence:
// translation lives in the locales, so a server-composed title would be
// untranslatable and would put presentation in the read model.
export type KindSlug =
  | 'gap'
  | 'tentativeInFrozen'
  | 'overcommit'
  | 'demandOnClosedRoot'
  | 'coverageOutOfWindow'
  | 'demandOnUndatedNode'
  | 'noDefaultCalendar'
  | 'inactiveWithCoverage'
  | 'underBand';

const SLUG: Record<SignalKind, KindSlug> = {
  [SignalKind.Gap]: 'gap',
  [SignalKind.TentativeInFrozen]: 'tentativeInFrozen',
  [SignalKind.Overcommit]: 'overcommit',
  [SignalKind.DemandOnClosedRoot]: 'demandOnClosedRoot',
  [SignalKind.CoverageOutOfWindow]: 'coverageOutOfWindow',
  [SignalKind.DemandOnUndatedNode]: 'demandOnUndatedNode',
  [SignalKind.NoDefaultCalendar]: 'noDefaultCalendar',
  [SignalKind.InactiveWithCoverage]: 'inactiveWithCoverage',
  [SignalKind.UnderBand]: 'underBand',
};

export const slugOf = (kind: SignalKind | undefined): KindSlug => SLUG[kind ?? SignalKind.Gap];

// Aggregate kinds have no subject to name and no deep link to follow: their verb
// opens the page where the occurrences live, not a specific row.
export const isAggregate = (s: SignalDto): boolean => s.tier !== SignalTier.Breach;

// ── Deep links (the other half of the admission rule) ────────────────────
export type DeepLink = { to: string; search?: Record<string, string> };

export function linkOf(signal: SignalDto): DeepLink {
  const link = signal.link ?? {};

  switch (signal.kind) {
    case SignalKind.Gap:
    case SignalKind.TentativeInFrozen:
      return link.rootProjectId
        ? { to: '/projects', search: { project: link.rootProjectId } }
        : { to: '/projects' };

    case SignalKind.Overcommit:
      return link.resourceId
        ? { to: '/people', search: { person: link.resourceId } }
        : { to: '/people' };

    case SignalKind.UnderBand:
      return { to: '/people' };

    case SignalKind.NoDefaultCalendar:
      return { to: '/time-config' };

    case SignalKind.InactiveWithCoverage:
      return { to: '/roles-teams' };

    // The remaining hygiene kinds are all about demands and their nodes.
    default:
      return { to: '/projects' };
  }
}

// Only ONE gesture is safe to complete without leaving the page: confirming a
// tentative is a state change that moves no hours. Everything else needs the
// context of the page it belongs to — which is exactly why the row links there
// instead of growing a mini staffing UI.
export const isInlineResolvable = (s: SignalDto): boolean =>
  s.kind === SignalKind.TentativeInFrozen && !!s.link?.allocationId;

// ── Change feed ──────────────────────────────────────────────────────────
export type ChangeVerb = 'new' | 'worsened' | 'crossed' | 'resolved';

// Order matters: a row that is both new and worse reads as new, and a resolved
// row is resolved whatever else happened to it on the way out.
// Takes a plain number, not `SignalChange`: the generated type is the union of
// the individual VALUES, and a bitmask combining two of them is in none of them.
// Narrowing the parameter would make every real combination a type error.
export function verbOf(change: number | undefined): ChangeVerb | null {
  const flags = change ?? SignalChange.None;
  if (flags & SignalChange.Resolved) return 'resolved';
  if (flags & SignalChange.Created) return 'new';
  if (flags & SignalChange.ZoneWorsened) return 'crossed';
  if (flags & SignalChange.MagnitudeWorsened) return 'worsened';
  return null;
}

export type FeedEntry = { entry: SignalChangeDto; verb: ChangeVerb };

export function feedOf(changes: readonly SignalChangeDto[], max: number): FeedEntry[] {
  return changes
    .map((entry) => ({ entry, verb: verbOf(entry.change) }))
    .filter((x): x is FeedEntry => x.verb !== null)
    .slice(0, max);
}

// ── Zone presentation ────────────────────────────────────────────────────
// A signal with no deadline carries no zone, and that is rendered as nothing
// rather than as a fourth chip: absence of a deadline is absence of urgency, and
// inventing a label for it is how the prototype ended up marking hygiene rows
// "frozen".
export type ZoneSlug = 'overdue' | 'frozen' | 'slushy' | 'liquid';

export function zoneSlugOf(zone: SignalZone | undefined): ZoneSlug | null {
  switch (zone) {
    case SignalZone.Overdue:
      return 'overdue';
    case SignalZone.Frozen:
      return 'frozen';
    case SignalZone.Slushy:
      return 'slushy';
    case SignalZone.Liquid:
      return 'liquid';
    default:
      return null;
  }
}
