import { createStyles } from 'antd-style';

// The one signal strip of the app. Deliberately compact: it lives INSIDE the
// page header row, so its height must stay under the title block's — vertical
// space belongs to the boards, not to the chrome.
export const useStyles = createStyles(({ token, css }) => ({
  row: css`
    display: flex;
    flex-wrap: wrap;
    gap: ${token.marginXS}px;
    align-items: stretch;
  `,
  card: css`
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 2px;
    padding: ${token.paddingXXS}px ${token.paddingSM}px;
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-inline-start: 3px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    /* Content-sized, never a full-width grid cell: that is what makes the
       strip "discreet" and leaves the header room for the title copy. */
    flex: 0 0 auto;
    white-space: nowrap;
  `,
  head: css`
    display: flex;
    align-items: baseline;
    gap: ${token.marginXS}px;
  `,
  value: css`
    font-size: ${token.fontSizeHeading4}px;
    font-weight: ${token.fontWeightStrong};
    font-variant-numeric: tabular-nums;
    line-height: 1.2;
    color: ${token.colorText};
  `,
  label: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
  hint: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    line-height: 1.2;
  `,

  // Tone modifiers: a closed enum, so they are real classes — not an inline
  // colour resolved from data.
  toneOk: css`
    border-inline-start-color: ${token.colorSuccess};
  `,
  toneOkValue: css`
    color: ${token.colorSuccess};
  `,
  toneWarning: css`
    border-inline-start-color: ${token.colorWarning};
  `,
  toneWarningValue: css`
    color: ${token.colorWarning};
  `,
  toneDanger: css`
    border-inline-start-color: ${token.colorError};
  `,
  toneDangerValue: css`
    color: ${token.colorError};
  `,
  toneNeutral: css`
    border-inline-start-color: ${token.colorBorder};
  `,
  toneNeutralValue: css`
    color: ${token.colorText};
  `,
}));
