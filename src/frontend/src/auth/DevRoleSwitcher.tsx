import { App, Segmented, Tooltip } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { useDevAccessActAs } from '@/api/generated/dev-access/dev-access';
import { AppRole } from '@/api/generated/schemas/appRole';
import { useAccess } from '@/auth/access';
import { useApiError } from '@/lib/errors';
import { useDevRoleSwitcherStyles } from '@/auth/auth.styles';

/**
 * Switches the developer's own application role (ADR-0030).
 *
 * It changes the real membership row through the real endpoint, so everything
 * downstream — policies, `/api/me`, every gated gesture — behaves exactly as it
 * will in production. That is the point: a config flag or a trusted header would
 * let dev exercise an arrangement that does not exist anywhere else.
 *
 * The backing endpoint is registered only in Development, so this control renders
 * only there too.
 */
export function DevRoleSwitcher() {
  const { t } = useTranslation();
  const { styles } = useDevRoleSwitcherStyles();
  const { role } = useAccess();
  const queryClient = useQueryClient();
  const { message } = App.useApp();
  const showApiError = useApiError();

  const actAs = useDevAccessActAs({
    mutation: {
      // Every gate in the UI reads /api/me, and the boards' contents do not
      // change — so resetting the whole cache is the honest way to re-render
      // against the new role rather than guessing at a key list.
      onSuccess: async () => {
        await queryClient.invalidateQueries();
        message.success(t('access.devRoleChanged'));
      },
      onError: (e) => showApiError(e),
    },
  });

  if (!import.meta.env.DEV) return null;
  if (role === null) return null;

  return (
    <Tooltip title={t('access.devRoleHint')}>
      <Segmented
        size="small"
        className={styles.switcher}
        value={role}
        disabled={actAs.isPending}
        onChange={(value) => actAs.mutate({ data: { role: value as AppRole } })}
        options={[
          { value: AppRole.Viewer, label: t('access.roleViewer') },
          { value: AppRole.Planner, label: t('access.rolePlanner') },
          { value: AppRole.Owner, label: t('access.roleOwner') },
        ]}
      />
    </Tooltip>
  );
}
