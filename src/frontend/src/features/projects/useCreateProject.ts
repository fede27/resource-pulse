import { useState } from 'react';
import { App } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getProjectNodesGetAllQueryKey,
  useProjectNodesCreate,
} from '@/api/generated/project-nodes/project-nodes';
import { getProjectsGetActiveInRangeQueryKey } from '@/api/generated/projects/projects';
import { ProjectNodeType } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import type { NewProjectSubmit } from './NewProjectPanel';

// Orchestrates the create gesture: one atomic POST for the root (planned dates
// included — backend applies them via Replan in the same SaveChanges), then one
// POST per phase, sequentially. A root failure keeps the panel open; a phase
// failure is surfaced but the project exists, so the board is refreshed anyway.
export function useCreateProject(onCreated: () => void) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const showApiError = useApiError();
  const queryClient = useQueryClient();
  const create = useProjectNodesCreate();
  const [saving, setSaving] = useState(false);

  const submit = async (values: NewProjectSubmit) => {
    setSaving(true);
    try {
      let rootId: string | undefined;
      try {
        const root = await create.mutateAsync({
          data: {
            nodeType: ProjectNodeType.Project,
            name: values.name,
            client: values.client,
            type: values.type,
            commitmentLevel: values.commitmentLevel,
            leadResourceId: values.ownerId,
            plannedStart: values.startISO,
            plannedEnd: values.endISO,
          },
        });
        rootId = root?.id ?? undefined;
      } catch (e) {
        showApiError(e);
        return;
      }

      let failedPhase: string | null = null;
      if (rootId) {
        for (const phase of values.phases) {
          try {
            await create.mutateAsync({
              data: {
                nodeType: ProjectNodeType.Phase,
                parentId: rootId,
                name: phase.name,
                plannedStart: phase.startISO,
                plannedEnd: phase.endISO,
              },
            });
          } catch (e) {
            showApiError(e);
            failedPhase = phase.name;
            break;
          }
        }
      }

      // Un-parameterized keys are prefixes: they match every fetched range.
      void queryClient.invalidateQueries({ queryKey: getProjectsGetActiveInRangeQueryKey() });
      void queryClient.invalidateQueries({ queryKey: getProjectNodesGetAllQueryKey() });

      if (failedPhase) {
        message.warning(
          t('projects.newProject.phaseCreateFailed', { name: values.name, phase: failedPhase }),
        );
      } else if (values.phases.length === 1) {
        message.success(t('projects.newProject.createSuccessOnePhase', { name: values.name }));
      } else if (values.phases.length > 1) {
        message.success(
          t('projects.newProject.createSuccessPhases', {
            name: values.name,
            count: values.phases.length,
          }),
        );
      } else {
        message.success(t('projects.newProject.createSuccess', { name: values.name }));
      }
      onCreated();
    } finally {
      setSaving(false);
    }
  };

  return { submit, saving };
}
