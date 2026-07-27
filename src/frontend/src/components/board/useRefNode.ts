import { useEffect, useState, type RefObject } from 'react';

// Mirrors a ref's element into state so hooks can DEPEND on the node.
//
// A ref's `.current` is not a reactive dependency: an effect keyed on the ref
// object runs once, and if the element mounts on a LATER render that effect never
// re-runs. Every board page renders a loading skeleton first and mounts its
// frame only once data arrives — so an effect that bails on `!ref.current`
// silently stayed inert forever, which is how the boards' windowing and the
// frame's height bound quietly stopped existing.
//
// The mirroring effect deliberately has NO dep array: it is one identity check
// per render and only sets state when the element actually changes, so it
// converges on the render after mount and cannot loop.
export function useRefNode<T extends HTMLElement>(ref: RefObject<T | null>): T | null {
  const [node, setNode] = useState<T | null>(null);
  // The dep-less form is the point, so do NOT take exhaustive-deps' suggestion of
  // `[ref, node]` here: with those deps the effect would not re-run on the render
  // that mounts the element (ref is stable, node is still null), which is the
  // exact bug this hook exists to fix. The guard makes it converge and prevents
  // the update chain the rule warns about.
  // eslint-disable-next-line react-hooks/exhaustive-deps -- must run after EVERY render; see above
  useEffect(() => {
    if (ref.current !== node) setNode(ref.current);
  });
  return node;
}
