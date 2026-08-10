import { useMemo } from 'react';

import { useMeGet } from '@/api/generated/me/me';
import { AppRole } from '@/api/generated/schemas/appRole';

/**
 * What the user is allowed to do, as capabilities rather than roles (ADR-0030).
 *
 * Call sites ask `can('plan')`, never `role === AppRole.Planner`: the three roles
 * are hierarchical and a comparison scattered across the UI is how a fourth role
 * would silently lose privileges it should have inherited. One place maps roles to
 * capabilities — here.
 */
export type Capability = 'plan' | 'administer';

export type Access = {
  role: AppRole | null;
  /** Whether a membership grants access to this tenant at all. */
  isMember: boolean;
  /** True until `/api/me` has answered; nothing is permitted meanwhile. */
  isLoading: boolean;
  can: (capability: Capability) => boolean;
};

/** Minimum role that satisfies each capability. The order is the hierarchy. */
const REQUIRED: Record<Capability, AppRole> = {
  plan: AppRole.Planner,
  administer: AppRole.Owner,
};

export function useAccess(): Access {
  const { data, isLoading } = useMeGet();

  return useMemo(() => {
    const role = data?.isMember ? (data.accessRole ?? null) : null;

    return {
      role,
      isMember: data?.isMember ?? false,
      isLoading,
      // Fails closed while loading and for a non-member: the server enforces this
      // anyway, so the only thing a permissive default buys is a button that
      // errors when pressed.
      can: (capability: Capability) => role !== null && role >= REQUIRED[capability],
    };
  }, [data, isLoading]);
}
