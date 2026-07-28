import type { ReactNode } from 'react';
import { useStyles } from './BoardToolbar.styles';

// THE control surface above every timed view (Progetti, Persone, Disponibilità):
// one card that carries the time filter and everything else that shapes the
// view — metric, grouping, search, sort, filters, result counts.
//
// Deliberately NOT an "action bar": nothing in here commands the domain. These
// controls only change what you are looking at, so they read as chrome, not as
// gestures, and a page's real actions belong in the page header instead.
//
// Rows are bands: the first one is always the time filter's grammar
// (`BoardTimeFilter` first, then whatever qualifies it); later rows carry
// row-shaping and result feedback. The hairline between bands belongs to the
// stack, so no row has to know its own index.
export function BoardToolbar({ children }: { children: ReactNode }) {
  const { styles } = useStyles();
  return <div className={styles.toolbar}>{children}</div>;
}

function Row({ children }: { children: ReactNode }) {
  const { styles } = useStyles();
  return <div className={styles.row}>{children}</div>;
}

/** Hairline between two control groups inside a row. */
function Divider() {
  const { styles } = useStyles();
  return <span className={styles.divider} />;
}

/** Pushes its content to the far end of the row. One per row. */
function Spacer({ children }: { children?: ReactNode }) {
  const { styles } = useStyles();
  return <span className={styles.spacer}>{children}</span>;
}

/**
 * Dim caption naming the control that follows it. `as="label"` when it wraps a
 * form control (a Switch, a Checkbox) so clicking the text toggles it.
 */
function Label({
  children,
  as = 'span',
  title,
}: {
  children: ReactNode;
  as?: 'span' | 'label';
  title?: string;
}) {
  const { styles } = useStyles();
  const Tag = as;
  return (
    <Tag className={styles.label} title={title}>
      {children}
    </Tag>
  );
}

/** "12 progetti di 40" — result feedback, tabular so it doesn't jitter. */
function Count({ children }: { children: ReactNode }) {
  const { styles } = useStyles();
  return <span className={styles.count}>{children}</span>;
}

BoardToolbar.Row = Row;
BoardToolbar.Divider = Divider;
BoardToolbar.Spacer = Spacer;
BoardToolbar.Label = Label;
BoardToolbar.Count = Count;
