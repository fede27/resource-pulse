// Reading the materialized path of a project node (ADR-0001): "/{rootId}/{childId}/…".
//
// Mirrors ProjectNodePath in the domain. The two cannot share code across the
// wire, so they share a name and this comment instead — if the format ever gains
// a segment, grep finds both.
export function rootIdFromPath(path: string | null | undefined): string {
  return path?.split('/').find((s) => s.length > 0) ?? '';
}
