/** Fixed-height placeholder standing in for a run of windowed-out rows. */
export function RowGap({ height }: { height: number }) {
  // dynamic: gap height = sum of the hidden rows' derived heights.
  return <div style={{ height }} aria-hidden />;
}
