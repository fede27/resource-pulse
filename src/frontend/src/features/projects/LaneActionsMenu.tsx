import { memo } from 'react';
import { Button, Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import { MoreOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import type { BoardProject, CoverageBlock, DemandRow, LaneAction } from './boardModel';

export type LaneActionsTarget =
  | { kind: 'person'; block: CoverageBlock; project: BoardProject }
  | { kind: 'hole'; demand: DemandRow; project: BoardProject };

export type LaneActionsMenuProps = {
  target: LaneActionsTarget;
  // Keyed by target so the page passes ONE stable callback to every lane
  // (ProjectRow is memoized).
  onAction: (action: LaneAction) => void;
};

// The kebab on an expanded lane: coverage-block gestures on a person lane,
// demand gestures on an open-role lane. Always rendered (design choice: the
// affordance is discoverable, not hover-hidden).
type MenuItems = NonNullable<MenuProps['items']>;

export const LaneActionsMenu = memo(function LaneActionsMenu({ target, onAction }: LaneActionsMenuProps) {
  const { t } = useTranslation();

  let items: MenuItems;
  if (target.kind === 'person') {
    items = [
      target.block.hard
        ? { key: 'demote', label: t('projects.lane.actions.demote') }
        : { key: 'promote', label: t('projects.lane.actions.promote') },
      { key: 'reassign', label: t('projects.lane.actions.reassign') },
      { key: 'retarget', label: t('projects.lane.actions.retarget') },
      { type: 'divider' },
      { key: 'remove', danger: true, label: t('projects.lane.actions.remove') },
    ];
  } else {
    const noDates = !target.project.from || !target.project.to;
    items = [
      {
        key: 'cover',
        label: t('projects.lane.actions.cover'),
        disabled: noDates,
        ...(noDates ? { title: t('projects.lane.actions.coverNoDates') } : {}),
      },
      { key: 'editDemand', label: t('projects.lane.actions.editDemand') },
      { type: 'divider' },
      { key: 'deleteDemand', danger: true, label: t('projects.lane.actions.deleteDemand') },
    ];
  }

  const onClick: NonNullable<MenuProps['onClick']> = ({ key, domEvent }) => {
    domEvent.stopPropagation();
    if (target.kind === 'person') {
      const k = key as 'promote' | 'demote' | 'remove' | 'reassign' | 'retarget';
      onAction({ kind: k, block: target.block, project: target.project });
    } else {
      const k = key as 'cover' | 'editDemand' | 'deleteDemand';
      onAction({ kind: k, demand: target.demand, project: target.project });
    }
  };

  const label =
    target.kind === 'person'
      ? t('projects.lane.actions.personMenuLabel')
      : t('projects.lane.actions.holeMenuLabel');

  return (
    <Dropdown trigger={['click']} placement="bottomRight" menu={{ items, onClick }}>
      <Button
        type="text"
        size="small"
        icon={<MoreOutlined />}
        aria-label={label}
        onClick={(e) => e.stopPropagation()}
      />
    </Dropdown>
  );
});
