import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  fieldPair: css`
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: ${token.marginSM}px;
  `,
  datePicker: css`
    width: 100%;
  `,
  phasesHeader: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-block: ${token.marginXS}px;
  `,
  phasesTitle: css`
    font-size: 13px;
    font-weight: 600;
  `,
  phasesHint: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextQuaternary};
    margin-block-end: ${token.marginXS}px;
  `,
  phaseRow: css`
    display: flex;
    gap: ${token.marginXS}px;
    align-items: flex-start;
    margin-block-end: ${token.marginXS}px;
    padding: ${token.paddingXS}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
  `,
  phaseFields: css`
    flex: 1;
    min-width: 0;

    .ant-form-item {
      margin-block-end: ${token.marginXS}px;
    }

    .ant-form-item:last-child {
      margin-block-end: 0;
    }
  `,
  phaseDates: css`
    display: flex;
    gap: ${token.marginXS}px;

    .ant-form-item {
      flex: 1;
    }
  `,
}));
