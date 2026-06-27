import { Button, Tag, Tooltip, Typography } from 'antd';
import { CheckOutlined, ClockCircleOutlined, CloseCircleOutlined, DeleteOutlined } from '@ant-design/icons';
import { createStyles } from 'antd-style';
import { useTranslation } from 'react-i18next';
import { SkillApprovalStatus, SkillLevel } from '@/api/generated/schemas';
import { SegmentedLevelControl } from '@/components/domain/SegmentedLevelControl';
import { getSkillLevelOptions } from './skillLevel';

const { Text } = Typography;

const useStyles = createStyles(({ token, css }) => ({
  // `--reveal` drives the delete icon's opacity; the row reveals it on hover so
  // there's no hover state in React.
  root: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    padding: ${token.paddingXS}px;
    margin: 0 -${token.marginXS}px;
    border-radius: ${token.borderRadius}px;
    background: transparent;
    transition: background ${token.motionDurationFast};
    --reveal: transparent;
    &:hover {
      background: ${token.colorFillQuaternary};
      --reveal: ${token.colorTextTertiary};
    }
  `,
  name: css`
    flex: 1;
    min-width: 0;
    font-size: ${token.fontSize}px;
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    flex-wrap: wrap;
  `,
  nameText: css`
    font-size: ${token.fontSize}px;
  `,
  chip: css`
    margin: 0;
  `,
  deleteBtn: css`
    transition: color ${token.motionDurationFast};
    && {
      color: var(--reveal);
    }
  `,
}));

export type PersonSkillRowProps = {
  name: string;
  level: SkillLevel;
  status: SkillApprovalStatus;
  onChangeLevel: (next: SkillLevel) => void;
  onRemove: () => void;
  busy?: boolean;
};

export function PersonSkillRow({
  name,
  level,
  status,
  onChangeLevel,
  onRemove,
  busy = false,
}: PersonSkillRowProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();

  const statusChip = (() => {
    switch (status) {
      case SkillApprovalStatus.Approved:
        return (
          <Tooltip title={t('people.skills.statusApprovedTooltip')}>
            <Tag color="success" icon={<CheckOutlined />} className={styles.chip}>
              {t('people.skills.statusApproved')}
            </Tag>
          </Tooltip>
        );
      case SkillApprovalStatus.Rejected:
        return (
          <Tooltip title={t('people.skills.statusRejectedTooltip')}>
            <Tag color="error" icon={<CloseCircleOutlined />} className={styles.chip}>
              {t('people.skills.statusRejected')}
            </Tag>
          </Tooltip>
        );
      case SkillApprovalStatus.Pending:
      default:
        return (
          <Tooltip title={t('people.skills.statusPendingTooltip')}>
            <Tag color="warning" icon={<ClockCircleOutlined />} className={styles.chip}>
              {t('people.skills.statusPending')}
            </Tag>
          </Tooltip>
        );
    }
  })();

  return (
    <div className={styles.root}>
      <span className={styles.name}>
        <Text className={styles.nameText}>{name}</Text>
        {statusChip}
      </span>
      <SegmentedLevelControl<SkillLevel>
        value={level}
        options={getSkillLevelOptions(t)}
        onChange={onChangeLevel}
        disabled={busy}
      />
      <Button
        type="text"
        size="small"
        icon={<DeleteOutlined />}
        onClick={onRemove}
        loading={busy}
        title={t('people.skills.removeAction')}
        className={styles.deleteBtn}
      />
    </div>
  );
}
