import type { ReactNode } from 'react';
import { Drawer } from 'antd';
import { INSPECTOR_SIZE, useStyles } from './InspectorDrawer.styles';

export type InspectorDrawerProps = {
  open: boolean;
  onClose: () => void;
  /** Pre-resolved string/node (pass `t('...')`, not a key). */
  title: ReactNode;
  /** Optional second line under the title (e.g. "the timeline stays interactive"). */
  subtitle?: ReactNode;
  /** Optional right-aligned action row (e.g. Annulla / Crea). */
  footer?: ReactNode;
  /** false = non-modal form panel: the board behind stays interactive. */
  mask?: boolean;
  children: ReactNode;
};

// The project's "Ispettore" surface: every right-hand slide-over (read
// inspectors and form panels alike) goes through this wrapper so size and
// behavior co-evolve instead of drifting per feature.
export function InspectorDrawer({
  open,
  onClose,
  title,
  subtitle,
  footer,
  mask = true,
  children,
}: InspectorDrawerProps) {
  const { styles } = useStyles();
  return (
    <Drawer
      open={open}
      onClose={onClose}
      size={INSPECTOR_SIZE}
      mask={mask}
      destroyOnHidden
      title={
        subtitle ? (
          <div>
            {title}
            <div className={styles.subtitle}>{subtitle}</div>
          </div>
        ) : (
          title
        )
      }
      {...(footer ? { footer: <div className={styles.footer}>{footer}</div> } : {})}
    >
      {children}
    </Drawer>
  );
}
