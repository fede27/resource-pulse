import { AxiosError } from 'axios';
import dayjs from 'dayjs';
import type { TFunction } from 'i18next';
import type { ProblemDetails } from '@/api/client';
import { BoundaryEdge, CommitmentLevel, ProblemCode, ProjectStatus } from '@/api/generated/schemas';

// The refusal contract (ADR-0037). A refusal the client can tell apart arrives
// as a ProblemDetails whose `type` is `urn:resourcepulse:problem:{code}`, with
// its pieces in `args` — in wire format (enums as integers), never a sentence.
// The server's `detail` is English and stays the fallback for an uncoded error.
//
// ProblemCode is generated from the server's catalogue. problemMessage switches
// on it exhaustively and names each locale key literally, so a code the server
// adds fails this build twice over: no branch, and (once given one) no sentence.

const TYPE_PREFIX = 'urn:resourcepulse:problem:';
const KNOWN = new Set<string>(Object.values(ProblemCode));

export type Problem = { code: ProblemCode; args: Record<string, unknown> };

// The coded refusal inside an API error, or null — for an uncoded error, for a
// code this client does not know yet (a newer server), and for anything that
// did not come back from the API.
export function problemOf(error: unknown): Problem | null {
  if (!(error instanceof AxiosError)) return null;
  const pd = error.response?.data as (ProblemDetails & { args?: unknown }) | undefined;
  const type = pd?.type;
  if (typeof type !== 'string' || !type.startsWith(TYPE_PREFIX)) return null;
  const code = type.slice(TYPE_PREFIX.length);
  if (!KNOWN.has(code)) return null;
  const args = pd?.args && typeof pd.args === 'object' ? (pd.args as Record<string, unknown>) : {};
  return { code: code as ProblemCode, args };
}

export function commitmentLabel(t: TFunction, level: CommitmentLevel): string {
  switch (level) {
    case CommitmentLevel.Exploratory:
      return t('projects.newProject.commitmentExploratory');
    case CommitmentLevel.Planned:
      return t('projects.newProject.commitmentPlanned');
    case CommitmentLevel.Committed:
      return t('projects.newProject.commitmentCommitted');
    default:
      return t('projects.newProject.commitmentCritical');
  }
}

const levelOf = (t: TFunction, value: unknown): string =>
  typeof value === 'number' ? commitmentLabel(t, value as CommitmentLevel) : t('problems.unspecifiedLevel');

const levelsOf = (t: TFunction, value: unknown): string =>
  Array.isArray(value) ? value.map((v) => `«${levelOf(t, v)}»`).join(', ') : '';

const text = (value: unknown): string => (typeof value === 'string' ? value : '');
const count = (value: unknown): number => (typeof value === 'number' ? value : 0);
const dateOf = (value: unknown): string => (typeof value === 'string' ? dayjs(value).format('D MMM YYYY') : '');
const edgeOf = (t: TFunction, value: unknown): string =>
  value === BoundaryEdge.End ? t('problems.edgeEnd') : t('problems.edgeStart');
const statusOf = (t: TFunction, value: unknown): string =>
  value === ProjectStatus.Cancelled ? t('problems.statusCancelled') : t('problems.statusClosed');

// What still holds a node: only the counts the server sent, and only non-zero.
function inUseOf(t: TFunction, args: Record<string, unknown>): string {
  const parts: string[] = [];
  if (count(args.children) > 0) parts.push(t('problems.inUseChildren', { n: count(args.children) }));
  if (count(args.demands) > 0) parts.push(t('problems.inUseDemands', { n: count(args.demands) }));
  if (count(args.coverages) > 0) parts.push(t('problems.inUseCoverages', { n: count(args.coverages) }));
  if (count(args.constraints) > 0) parts.push(t('problems.inUseConstraints', { n: count(args.constraints) }));
  return parts.join(', ');
}

// The user's sentence for a coded refusal: the pieces turned into words, then
// the code's own sentence. Exhaustive on purpose — a code added to the catalogue
// without a branch here stops this function compiling ("lacks ending return").
export function problemMessage(t: TFunction, problem: Problem): string {
  const { args } = problem;
  switch (problem.code) {
    case ProblemCode.HardRequiresCommitment:
      return t('problems.codes.hard-requires-commitment', {
        level: levelOf(t, args.level),
        required: levelsOf(t, args.required),
      });
    case ProblemCode.CommitmentDowngradeDemotesHard:
      return t('problems.codes.commitment-downgrade-demotes-hard', {
        from: levelOf(t, args.from),
        to: levelOf(t, args.to),
        count: count(args.count),
      });
    case ProblemCode.ProjectRootClosed:
      return t('problems.codes.project-root-closed', { project: text(args.project), status: statusOf(t, args.status) });
    case ProblemCode.ResourceInactive:
      return t('problems.codes.resource-inactive', { resource: text(args.resource) });
    case ProblemCode.NodeHasNoPlannedDate:
      return t('problems.codes.node-has-no-planned-date', { node: text(args.node), edge: edgeOf(t, args.edge) });
    case ProblemCode.PlannedDateFollowed:
      return t('problems.codes.planned-date-followed', {
        node: text(args.node),
        edge: edgeOf(t, args.edge),
        count: count(args.count),
      });
    case ProblemCode.FollowWouldInvertCoverage:
      return t('problems.codes.follow-would-invert-coverage', {
        node: text(args.node),
        resource: text(args.resource),
        from: dateOf(args.from),
        to: dateOf(args.to),
      });
    case ProblemCode.ZeroCapacityInWindow:
      return t('problems.codes.zero-capacity-in-window', {
        resource: text(args.resource),
        from: dateOf(args.from),
        to: dateOf(args.to),
      });
    case ProblemCode.HoursAbovePercentCap:
      return t('problems.codes.hours-above-percent-cap', {
        percent: Math.round(count(args.percent)),
        cap: count(args.cap),
      });
    case ProblemCode.DemandHasCoverage:
      return t('problems.codes.demand-has-coverage', { count: count(args.count) });
    case ProblemCode.NodeHasFollowers:
      return t('problems.codes.node-has-followers', {
        count: count(args.count),
        operation: args.operation === 'rollUp' ? t('problems.operationRollUp') : t('problems.operationReplan'),
      });
    case ProblemCode.NodeInUse:
      return t('problems.codes.node-in-use', { node: text(args.node), what: inUseOf(t, args) });
    case ProblemCode.RoleInUse:
      return t('problems.codes.role-in-use', {
        role: text(args.role),
        resources: count(args.resources),
        demands: count(args.demands),
      });
    case ProblemCode.TeamInUse:
      return t('problems.codes.team-in-use', { team: text(args.team), resources: count(args.resources) });
  }
}
