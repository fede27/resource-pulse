import { describe, expect, it } from 'vitest';

import {
  SignalChange,
  SignalKind,
  SignalTier,
  SignalZone,
  type SignalChangeDto,
  type SignalDto,
  type SignalSweepDto,
} from '@/api/generated/schemas';
import {
  feedOf,
  isAggregate,
  isInlineResolvable,
  linkOf,
  queueOf,
  slugOf,
  sweepStatus,
  toneOf,
  verbOf,
  verdictOf,
  zoneSlugOf,
} from './dashboardModel';

const NOW = new Date('2026-06-24T09:00:00Z');

const signal = (over: Partial<SignalDto> = {}): SignalDto => ({
  id: crypto.randomUUID(),
  kind: SignalKind.Gap,
  tier: SignalTier.Breach,
  magnitude: 10,
  ...over,
});

describe('sweepStatus — three states, not two', () => {
  // An empty queue is only "the plan holds" if we know we looked. On a tenant the
  // detector has never visited, that sentence would be a lie.
  it('reports "never" when the detector has not run', () => {
    expect(sweepStatus({ lastSweptAt: null } as SignalSweepDto, NOW)).toBe('never');
    expect(sweepStatus(undefined, NOW)).toBe('never');
  });

  it('reports "fresh" inside the advertised tolerance', () => {
    const sweep = {
      lastSweptAt: '2026-06-24T06:00:00Z',
      staleAfterHours: 48,
    } as SignalSweepDto;
    expect(sweepStatus(sweep, NOW)).toBe('fresh');
  });

  it('reports "stale" past it — a stuck worker must not look like a healthy plan', () => {
    const sweep = {
      lastSweptAt: '2026-06-18T06:00:00Z',
      staleAfterHours: 48,
    } as SignalSweepDto;
    expect(sweepStatus(sweep, NOW)).toBe('stale');
  });
});

describe('verdictOf', () => {
  it('sums gap HOURS, not gap rows', () => {
    // One 4-hour gap and one 400-hour gap are not "two gaps" — hours are the
    // reconciliation truth (ADR-0026).
    const v = verdictOf([
      signal({ kind: SignalKind.Gap, magnitude: 4 }),
      signal({ kind: SignalKind.Gap, magnitude: 400 }),
    ]);
    expect(v.gaps).toBe(2);
    expect(v.gapHours).toBe(404);
  });

  it('is clean when nothing is a breach', () => {
    const v = verdictOf([
      signal({ kind: SignalKind.UnderBand, tier: SignalTier.Slack }),
      signal({ kind: SignalKind.NoDefaultCalendar, tier: SignalTier.Hygiene }),
    ]);
    expect(v.clean).toBe(true);
  });

  it('is not clean with a single breach, however small', () => {
    expect(verdictOf([signal({ magnitude: 0.5 })]).clean).toBe(false);
  });

  it('counts accepted risks separately — accepted is not resolved', () => {
    const v = verdictOf([signal({ isAcknowledged: true }), signal()]);
    expect(v.acknowledged).toBe(1);
    expect(v.gaps).toBe(2);
  });
});

describe('queueOf — a fixed budget, and an honest overflow', () => {
  it('shows at most the budget and states the rest', () => {
    const q = queueOf(Array.from({ length: 11 }, () => signal()), 7);
    expect(q.shown).toHaveLength(7);
    expect(q.total).toBe(11);
    expect(q.overflow).toBe(4);
  });

  it('preserves the server ranking — the client never reorders', () => {
    const a = signal({ magnitude: 1 });
    const b = signal({ magnitude: 999 });
    expect(queueOf([a, b], 7).shown.map((s) => s.id)).toEqual([a.id, b.id]);
  });

  it('has no overflow below the budget', () => {
    expect(queueOf([signal()], 7).overflow).toBe(0);
  });
});

describe('verbOf — precedence between simultaneous flags', () => {
  it('reads a resolved row as resolved whatever else happened on the way out', () => {
    expect(verbOf(SignalChange.Resolved | SignalChange.MagnitudeWorsened)).toBe('resolved');
  });

  it('reads a brand-new row as new, not as worse', () => {
    expect(verbOf(SignalChange.Created | SignalChange.ZoneWorsened)).toBe('new');
  });

  it('maps the two worsening flags', () => {
    expect(verbOf(SignalChange.ZoneWorsened)).toBe('crossed');
    expect(verbOf(SignalChange.MagnitudeWorsened)).toBe('worsened');
  });

  it('yields nothing for None — an improvement is not news', () => {
    expect(verbOf(SignalChange.None)).toBeNull();
  });
});

describe('feedOf', () => {
  it('drops entries with no verb and caps the rest', () => {
    const entries: SignalChangeDto[] = [
      { signalId: '1', change: SignalChange.Created },
      { signalId: '2', change: SignalChange.None },
      { signalId: '3', change: SignalChange.Resolved },
      { signalId: '4', change: SignalChange.MagnitudeWorsened },
    ];
    const feed = feedOf(entries, 2);
    expect(feed.map((f) => f.entry.signalId)).toEqual(['1', '3']);
  });
});

describe('presentation', () => {
  it('gives every kind a readable slug, because the wire carries numbers', () => {
    expect(slugOf(SignalKind.Gap)).toBe('gap');
    expect(slugOf(SignalKind.InactiveWithCoverage)).toBe('inactiveWithCoverage');
  });

  it('tones by kind, so a liquid gap still reads as a gap', () => {
    expect(toneOf(SignalKind.Gap)).toBe('danger');
    expect(toneOf(SignalKind.Overcommit)).toBe('caution');
    expect(toneOf(SignalKind.NoDefaultCalendar)).toBe('neutral');
  });

  it('renders no zone at all when there is no deadline', () => {
    // Absence of urgency, rendered as absence — not as a fourth chip.
    expect(zoneSlugOf(undefined)).toBeNull();
    expect(zoneSlugOf(SignalZone.Overdue)).toBe('overdue');
  });

  it('treats everything below a breach as aggregated', () => {
    expect(isAggregate(signal({ tier: SignalTier.Breach }))).toBe(false);
    expect(isAggregate(signal({ tier: SignalTier.Hygiene }))).toBe(true);
    expect(isAggregate(signal({ tier: SignalTier.Slack }))).toBe(true);
  });
});

describe('the inline gesture is exactly one', () => {
  it('allows confirming a tentative — a state change that moves no hours', () => {
    expect(
      isInlineResolvable(
        signal({ kind: SignalKind.TentativeInFrozen, link: { allocationId: 'a1' } }),
      ),
    ).toBe(true);
  });

  it('refuses a gap: choosing who covers needs the page with the context', () => {
    expect(isInlineResolvable(signal({ kind: SignalKind.Gap, link: { demandId: 'd1' } }))).toBe(
      false,
    );
  });

  it('refuses a tentative with no allocation to act on', () => {
    expect(isInlineResolvable(signal({ kind: SignalKind.TentativeInFrozen }))).toBe(false);
  });
});

describe('linkOf — every row has somewhere to go', () => {
  it('sends a gap to the project board', () => {
    expect(linkOf(signal({ kind: SignalKind.Gap, link: { rootProjectId: 'p1' } }))).toEqual({
      to: '/projects',
      search: { project: 'p1' },
    });
  });

  it('sends an overcommit to the people board', () => {
    expect(linkOf(signal({ kind: SignalKind.Overcommit, link: { resourceId: 'r1' } })).to).toBe(
      '/people',
    );
  });

  it('sends the calendar hygiene row to the time configuration', () => {
    expect(linkOf(signal({ kind: SignalKind.NoDefaultCalendar })).to).toBe('/time-config');
  });

  it('never leaves a row without a destination', () => {
    for (const kind of Object.values(SignalKind)) {
      expect(linkOf(signal({ kind })).to).toMatch(/^\//);
    }
  });
});
