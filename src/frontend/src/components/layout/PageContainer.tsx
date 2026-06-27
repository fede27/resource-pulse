import type { ReactNode } from 'react';
import { createStyles } from 'antd-style';

export type PageContainerProps = {
  children: ReactNode;
  /**
   * Drop the centred max-width cap and let the content fill the viewport.
   * Used by full-bleed screens like the teams load heatmap, which scroll their
   * own timeline horizontally.
   */
  fullBleed?: boolean;
};

const useStyles = createStyles(({ token, css }) => ({
  root: css`
    padding: ${token.pageGutter}px;
    max-width: ${token.pageMaxWidth}px;
  `,
  fullBleed: css`
    padding: ${token.pageGutter}px;
  `,
}));

/**
 * Standard padded page wrapper for routed content. The page gutter and max
 * content width are tokens (single-sourced design decisions), so routes no
 * longer repeat `padding: 24 / maxWidth: 1440` inline.
 */
export function PageContainer({ children, fullBleed = false }: PageContainerProps) {
  const { styles } = useStyles();
  return <div className={fullBleed ? styles.fullBleed : styles.root}>{children}</div>;
}
