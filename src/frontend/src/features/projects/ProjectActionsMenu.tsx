import { memo } from 'react';
import { Button, Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import { CheckOutlined, MoreOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CommitmentLevel } from '@/api/generated/schemas';
import { availableActionKinds, type BoardProject, type ProjectAction } from './boardModel';

export type ProjectActionsMenuProps = {
  project: BoardProject;
  // Keyed by the project so the page can pass ONE stable callback to every
  // row (ProjectRow is memoized).
  onAction: (project: BoardProject, action: ProjectAction) => void;
};

type CommitmentLabelKey =
  | 'projects.newProject.commitmentExploratory'
  | 'projects.newProject.commitmentPlanned'
  | 'projects.newProject.commitmentCommitted'
  | 'projects.newProject.commitmentCritical';

const COMMITMENT_LEVELS: { level: CommitmentLevel; labelKey: CommitmentLabelKey }[] = [
  { level: CommitmentLevel.Exploratory, labelKey: 'projects.newProject.commitmentExploratory' },
  { level: CommitmentLevel.Planned, labelKey: 'projects.newProject.commitmentPlanned' },
  { level: CommitmentLevel.Committed, labelKey: 'projects.newProject.commitmentCommitted' },
  { level: CommitmentLevel.Critical, labelKey: 'projects.newProject.commitmentCritical' },
];

// The kebab of contextual actions on a project root (design PoC: kebab →
// dropdown, divider before the danger zone, consequential actions confirm).
// Items follow the domain state machine; terminal states render nothing.
export const ProjectActionsMenu = memo(function ProjectActionsMenu({
  project,
  onAction,
}: ProjectActionsMenuProps) {
  const { t } = useTranslation();
  const kinds = availableActionKinds(project.status);
  if (kinds.length === 0) return null;

  const items: MenuProps['items'] = [];
  if (kinds.includes('start')) items.push({ key: 'start', label: t('projects.actions.start') });
  if (kinds.includes('resume')) items.push({ key: 'resume', label: t('projects.actions.resume') });
  if (kinds.includes('suspend')) items.push({ key: 'suspend', label: t('projects.actions.suspend') });
  if (kinds.includes('complete')) items.push({ key: 'complete', label: t('projects.actions.complete') });
  if (kinds.includes('setCommitment')) {
    items.push({
      key: 'commitment',
      label: t('projects.actions.commitment'),
      children: COMMITMENT_LEVELS.map(({ level, labelKey }) => ({
        key: `commitment:${level}`,
        label: t(labelKey),
        disabled: level === project.commitmentLevel,
        icon: level === project.commitmentLevel ? <CheckOutlined /> : undefined,
      })),
    });
  }
  if (kinds.includes('cancel')) {
    items.push({ type: 'divider' }, { key: 'cancel', danger: true, label: t('projects.actions.cancel') });
  }

  const onClick: NonNullable<MenuProps['onClick']> = ({ key, domEvent }) => {
    domEvent.stopPropagation();
    if (key.startsWith('commitment:')) {
      onAction(project, {
        kind: 'setCommitment',
        level: Number(key.slice('commitment:'.length)) as CommitmentLevel,
      });
      return;
    }
    onAction(project, { kind: key as Exclude<ProjectAction['kind'], 'setCommitment'> });
  };

  return (
    <Dropdown trigger={['click']} placement="bottomRight" menu={{ items, onClick }}>
      <Button
        type="text"
        size="small"
        icon={<MoreOutlined />}
        aria-label={t('projects.actions.menuLabel')}
        onClick={(e) => e.stopPropagation()}
      />
    </Dropdown>
  );
});
