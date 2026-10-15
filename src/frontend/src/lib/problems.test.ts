import { describe, expect, it } from 'vitest';
import { AxiosError } from 'axios';
import i18n from '@/i18n';
import { BoundaryEdge, CommitmentLevel, ProblemCode, ProjectStatus } from '@/api/generated/schemas';
import { problemMessage, problemOf } from './problems';

// The refusal contract on the client (ADR-0037). The compiler already proves
// every code has a branch and a sentence; these prove the sentence comes out
// whole — every placeholder filled, every piece translated.

const t = i18n.t.bind(i18n);

// Representative pieces per code, in wire format (as ProblemCodeSitesTests
// asserts them on the server).
const ARGS: Record<ProblemCode, Record<string, unknown>> = {
  [ProblemCode.HardRequiresCommitment]: { level: CommitmentLevel.Planned, required: [3, 4] },
  [ProblemCode.CommitmentDowngradeDemotesHard]: { from: 3, to: 2, count: 3 },
  [ProblemCode.ProjectRootClosed]: { project: 'Portale', status: ProjectStatus.Cancelled },
  [ProblemCode.ResourceInactive]: { resource: 'Anna Neri' },
  [ProblemCode.NodeHasNoPlannedDate]: { node: 'Analisi', edge: BoundaryEdge.End },
  [ProblemCode.PlannedDateFollowed]: { node: 'Analisi', edge: BoundaryEdge.Start, count: 4 },
  [ProblemCode.FollowWouldInvertCoverage]: {
    node: 'Analisi',
    resource: 'Anna Neri',
    from: '2026-06-01',
    to: '2026-06-10',
  },
  [ProblemCode.ZeroCapacityInWindow]: { resource: 'Anna Neri', from: '2026-08-10', to: '2026-08-21' },
  [ProblemCode.HoursAbovePercentCap]: { percent: 2000, cap: 1000 },
  [ProblemCode.DemandHasCoverage]: { count: 2 },
  [ProblemCode.NodeHasFollowers]: { count: 2, operation: 'rollUp' },
  [ProblemCode.NodeInUse]: { node: 'Portale', demands: 1, coverages: 3 },
  [ProblemCode.RoleInUse]: { role: 'QA Engineer', resources: 4, demands: 2 },
  [ProblemCode.TeamInUse]: { team: 'Platform', resources: 5 },
};

describe('problemMessage', () => {
  it.each(Object.values(ProblemCode))('%s comes out whole', (code) => {
    const sentence = problemMessage(t, { code, args: ARGS[code] });

    expect(sentence).not.toMatch(/\{\{|\}\}/);
    expect(sentence).not.toMatch(/problems\./); // a missing key would echo itself
    expect(sentence).not.toMatch(/«»/); // an empty piece
  });

  it('translates enum pieces and dates', () => {
    expect(problemMessage(t, { code: ProblemCode.PlannedDateFollowed, args: ARGS['planned-date-followed'] })).toBe(
      'La data di inizio di «Analisi» non si può togliere: la seguono 4 bordi di copertura. Fissali prima, poi riprova.',
    );
    expect(
      problemMessage(t, { code: ProblemCode.FollowWouldInvertCoverage, args: ARGS['follow-would-invert-coverage'] }),
    ).toContain('(1 giu 2026 – 10 giu 2026)');
    expect(problemMessage(t, { code: ProblemCode.ProjectRootClosed, args: ARGS['project-root-closed'] })).toBe(
      'Il progetto «Portale» è annullato: il suo piano non si modifica più.',
    );
  });

  it('lists only what holds a node, and only what the server counted', () => {
    expect(problemMessage(t, { code: ProblemCode.NodeInUse, args: ARGS['node-in-use'] })).toBe(
      'Non si può eliminare «Portale», che porta ancora domande: 1, coperture: 3. Toglili prima, poi riprova.',
    );
  });
});

describe('problemOf', () => {
  const axiosWith = (data: unknown) => {
    const err = new AxiosError('x');
    // @ts-expect-error minimal response shape
    err.response = { data };
    return err;
  };

  it('reads the code and the pieces', () => {
    expect(problemOf(axiosWith({ type: 'urn:resourcepulse:problem:team-in-use', args: { resources: 2 } }))).toEqual({
      code: ProblemCode.TeamInUse,
      args: { resources: 2 },
    });
  });

  it('is null for an uncoded error, an unknown code, the auth types and a non-API error', () => {
    expect(problemOf(axiosWith({ detail: 'x' }))).toBeNull();
    expect(problemOf(axiosWith({ type: 'urn:resourcepulse:problem:from-the-future', args: {} }))).toBeNull();
    expect(problemOf(axiosWith({ type: 'urn:resourcepulse:problem:insufficient-role' }))).toBeNull();
    expect(problemOf(new Error('boom'))).toBeNull();
  });
});
