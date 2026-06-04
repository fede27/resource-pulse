import { useState } from 'react';
import { Button, Tag, theme, Tooltip, Typography } from 'antd';
import { CheckOutlined, ClockCircleOutlined, CloseCircleOutlined, DeleteOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { SkillApprovalStatus, SkillLevel } from '@/api/generated/schemas';
import { SegmentedLevelControl } from '@/components/domain/SegmentedLevelControl';
import { getSkillLevelOptions } from './skillLevel';

const { Text } = Typography;

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
  const { token } = theme.useToken();
  const [hover, setHover] = useState(false);

  const statusChip = (() => {
    switch (status) {
      case SkillApprovalStatus.Approved:
        return (
          <Tooltip title={t('people.skills.statusApprovedTooltip')}>
            <Tag color="success" icon={<CheckOutlined />} style={{ margin: 0 }}>
              {t('people.skills.statusApproved')}
            </Tag>
          </Tooltip>
        );
      case SkillApprovalStatus.Rejected:
        return (
          <Tooltip title={t('people.skills.statusRejectedTooltip')}>
            <Tag color="error" icon={<CloseCircleOutlined />} style={{ margin: 0 }}>
              {t('people.skills.statusRejected')}
            </Tag>
          </Tooltip>
        );
      case SkillApprovalStatus.Pending:
      default:
        return (
          <Tooltip title={t('people.skills.statusPendingTooltip')}>
            <Tag color="warning" icon={<ClockCircleOutlined />} style={{ margin: 0 }}>
              {t('people.skills.statusPending')}
            </Tag>
          </Tooltip>
        );
    }
  })();

  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        padding: '8px 8px',
        margin: '0 -8px',
        borderRadius: token.borderRadius,
        background: hover ? token.colorFillQuaternary : 'transparent',
        transition: `background ${token.motionDurationFast}`,
      }}
    >
      <span
        style={{
          flex: 1,
          minWidth: 0,
          fontSize: 14,
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          flexWrap: 'wrap',
        }}
      >
        <Text style={{ fontSize: 14 }}>{name}</Text>
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
        style={{
          color: hover ? token.colorTextTertiary : 'transparent',
          transition: `color ${token.motionDurationFast}`,
        }}
      />
    </div>
  );
}
