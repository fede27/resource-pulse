/**
 * The `type` values the API uses to tell its 403s apart (ADR-0030).
 *
 * Two refusals that look identical on the wire need different words and have
 * different fixes: an unmapped organization is an operator problem, an
 * insufficient role is expected and usually means the UI should have hidden the
 * gesture. Branch on `type` — the title is display copy and free to change.
 */
export const AuthProblemType = {
  tenantNotResolved: 'urn:timesloth:problem:tenant-not-resolved',
  notAMember: 'urn:timesloth:problem:not-a-member',
  insufficientRole: 'urn:timesloth:problem:insufficient-role',
} as const;
